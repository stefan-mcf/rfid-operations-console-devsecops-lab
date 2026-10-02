"""Isolated release tests using simulated Docker/HTTP and real local Git repositories."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
FAKE = r'''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
p = Path(os.environ['SIMULATION_STATE'])
s = json.loads(p.read_text()); a = sys.argv[1:]; name = Path(sys.argv[0]).name
def save(): p.write_text(json.dumps(s))
def image(name):
    old = name == 'rfid-ops:release-old'
    return {'Id': 'sha256:old' if old else 'sha256:new',
            'Config': {'Labels': {'org.opencontainers.image.version': 'old' if old else 'new',
                                  'org.opencontainers.image.revision': 'old-commit' if old else 'new-commit'}}}
if name == 'sleep': sys.exit(0)
if name == 'docker':
    s['calls'].append(a); save()
    if a[0] == 'inspect':
        if not s['current']: sys.exit(1)
        print(image(s['current'])['Id'] if a[-1] == '{{.Image}}' else s['current'])
    elif a[:2] == ['image', 'inspect']:
        data = image(a[2])
        if '--format' not in a: print(json.dumps([data]))
        elif a[-1] == '{{.Id}}': print(data['Id'])
        elif 'version' in a[-1]: print(data['Config']['Labels']['org.opencontainers.image.version'])
        else: print(data['Config']['Labels']['org.opencontainers.image.revision'])
    elif a[0] == 'tag': pass
    elif a[0] == 'compose':
        if 'up' in a:
            s['current'] = os.environ['RFID_OPS_IMAGE']; s['healthy'] = True
            if os.environ.get('SIMULATION_FAIL_CANDIDATE') == '1' and s['current'] != 'rfid-ops:release-old': s['healthy'] = False
            if os.environ.get('SIMULATION_FAIL_ROLLBACK') == '1' and s['current'] == 'rfid-ops:release-old': s['healthy'] = False
            save()
        elif 'stop' in a: s['healthy'] = False; save()
        elif 'ps' in a: print(json.dumps({'Image': s['current'], 'Health': 'healthy' if s['healthy'] else 'unhealthy'}))
        else: sys.exit('Unexpected compose command')
    else: sys.exit('Unexpected Docker command')
elif name == 'curl':
    url = next(x for x in a if x.startswith('http://'))
    if not s['healthy']: sys.exit(7)
    if url.endswith('/health'): print(json.dumps({'status': 'healthy', 'database': 'reachable'}))
    elif url.endswith('/metrics'): print('rfid_ops_access_decisions_total 1')
    elif '/tags/' in url: print(json.dumps({'state': 'Active'}))
    elif url.endswith('/api/v1/events'): print(json.dumps({'outcome': 'Granted', 'reason': 'ActiveRegistration'}))
    elif url.endswith('/'): print('RFID OPERATIONS / LOCAL SIMULATION <form id="tag-form"></form><form id="event-form"></form>')
    else: sys.exit('Unexpected URL')
else: sys.exit('Unexpected command')
'''


class ReleaseTests(unittest.TestCase):
    def setUp(self):
        (ROOT / '.pipeline').mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='release-tests-', dir=ROOT / '.pipeline')
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.repo = self.base / 'checkout'
        self.repo.mkdir()
        shutil.copytree(ROOT / 'scripts/ci', self.repo / 'scripts/ci')
        bin_path = self.base / 'bin'
        bin_path.mkdir()
        for name in ('docker', 'curl', 'sleep'):
            f = bin_path / name
            f.write_text(FAKE)
            f.chmod(0o700)
        self.state = self.base / 'state.json'
        self.state.write_text(json.dumps({'current': 'rfid-ops:release-old', 'healthy': True, 'calls': []}))
        self.env = dict(os.environ, PATH=str(bin_path) + os.pathsep + os.environ['PATH'],
                        SIMULATION_STATE=str(self.state), RFID_OPS_VERSION='1.0.15-test',
                        RFID_OPS_READER_KEY='test-reader', RFID_OPS_ADMIN_KEY='test-admin',
                        RFID_OPS_HOST_ADDRESS='127.0.0.1', GIT_RELEASE_USER='test', GIT_RELEASE_TOKEN='test')
        self.git('init', '--quiet')
        self.git('config', 'user.name', 'Isolated Test')
        self.git('config', 'user.email', 'test@localhost')
        self.git('add', '.')
        self.git('commit', '--quiet', '-m', 'Fixture')
        self.commit = self.git('rev-parse', 'HEAD').strip()
        self.env['GIT_COMMIT'] = self.commit
        remote = self.base / 'remote.git'
        subprocess.run(['git', 'init', '--bare', '--quiet', str(remote)], check=True)
        self.git('remote', 'add', 'origin', str(remote))

    def git(self, *args):
        return subprocess.check_output(['git', '-C', str(self.repo), *args], text=True, stderr=subprocess.PIPE)

    def run_script(self, name, success=True):
        result = subprocess.run(['bash', f'scripts/ci/{name}'], cwd=self.repo, env=self.env,
                                capture_output=True, text=True, timeout=30)
        if success:
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        else:
            self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(list((self.repo / '.pipeline').glob('git-askpass.*')))
        return result

    def json(self, relative):
        return json.loads((self.repo / relative).read_text())

    def remote_tags(self):
        return self.git('ls-remote', '--tags', 'origin')

    def test_success_pushes_annotated_tag_for_exact_commit(self):
        self.run_script('release.sh')
        manifest = self.json('artifacts/release/release-manifest.json')
        self.assertEqual(manifest['commit'], self.commit)
        self.assertEqual(manifest['previousImage'], 'rfid-ops:release-old')
        self.assertEqual(manifest['imageId'], 'sha256:new')
        self.assertEqual(self.git('cat-file', '-t', 'v1.0.15-test').strip(), 'tag')
        self.assertIn(self.commit + '\trefs/tags/v1.0.15-test^{}', self.remote_tags())
        self.assertTrue(self.json('artifacts/release/git-tag.json')['remoteVerified'])

    def test_drill_enters_error_handler_restores_previous_and_never_tags(self):
        self.run_script('rollback-drill.sh')
        self.assertEqual(self.json('artifacts/rollback-drill/drill-manifest.json')['status'], 'passed')
        manifest = self.json('artifacts/rollback-drill/rollback/rollback-manifest.json')
        self.assertEqual(manifest['imageId'], 'sha256:old')
        self.assertEqual(manifest['version'], 'old')
        self.assertEqual(json.loads(self.state.read_text())['current'], 'rfid-ops:release-old')
        self.assertEqual(self.remote_tags(), '')

    def test_smoke_failure_restores_previous_without_publishing(self):
        self.env['SIMULATION_FAIL_CANDIDATE'] = '1'
        self.run_script('release.sh', success=False)
        self.assertEqual(self.json('artifacts/release/rollback/rollback-manifest.json')['status'], 'restored')
        self.assertEqual(self.remote_tags(), '')

    def test_conflicting_remote_tag_is_immutable_and_release_restores_previous(self):
        self.git('tag', '-a', 'v1.0.15-test', '-m', 'Existing release')
        self.git('push', '--quiet', 'origin', 'refs/tags/v1.0.15-test')
        before = self.remote_tags()
        (self.repo / 'another-source.txt').write_text('new source')
        self.git('add', 'another-source.txt')
        self.git('commit', '--quiet', '-m', 'Another source')
        self.env['GIT_COMMIT'] = self.git('rev-parse', 'HEAD').strip()
        self.run_script('release.sh', success=False)
        self.assertEqual(self.remote_tags(), before)
        self.assertEqual(self.json('artifacts/release/rollback/rollback-manifest.json')['status'], 'restored')

    def test_drill_does_not_claim_success_when_restore_smoke_fails(self):
        self.env['SIMULATION_FAIL_ROLLBACK'] = '1'
        self.run_script('rollback-drill.sh', success=False)
        self.assertFalse((self.repo / 'artifacts/rollback-drill/drill-manifest.json').exists())

    def test_first_release_has_no_rollback_to_demonstrate(self):
        self.state.write_text(json.dumps({'current': None, 'healthy': False, 'calls': []}))
        self.run_script('rollback-drill.sh')
        self.assertEqual(self.json('artifacts/rollback-drill/drill-manifest.json')['status'], 'skipped')
        self.assertEqual(self.remote_tags(), '')


if __name__ == '__main__':
    unittest.main(verbosity=2)
