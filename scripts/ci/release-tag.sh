#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"

require_value RFID_OPS_VERSION
require_value GIT_RELEASE_USER
require_value GIT_RELEASE_TOKEN
set +x

tag="v${RFID_OPS_VERSION}"
commit="$(git rev-parse HEAD)"
test "$commit" = "${GIT_COMMIT:-$commit}"
git check-ref-format "refs/tags/$tag"
evidence_directory="${RFID_OPS_RELEASE_EVIDENCE_DIRECTORY:-artifacts/release}"
mkdir -p "$evidence_directory"

askpass="$(mktemp .pipeline/git-askpass.XXXXXX)"
trap 'rm -f "$askpass"' EXIT
cat > "$askpass" <<'ASKPASS'
#!/usr/bin/env bash
case "$1" in
  *Username*) printf '%s\n' "$GIT_RELEASE_USER" ;;
  *Password*) printf '%s\n' "$GIT_RELEASE_TOKEN" ;;
  *) exit 1 ;;
esac
ASKPASS
chmod 700 "$askpass"
export GIT_ASKPASS="$project_root/$askpass"
export GIT_TERMINAL_PROMPT=0

remote_tag="$(git ls-remote --tags origin "refs/tags/$tag")"
if [[ -n "$remote_tag" ]]; then
  git fetch --no-tags origin "refs/tags/$tag:refs/tags/$tag"
fi
if git rev-parse --verify "refs/tags/$tag" >/dev/null 2>&1; then
  test "$(git cat-file -t "refs/tags/$tag")" = tag
  test "$(git rev-parse "$tag^{commit}")" = "$commit"
else
  git -c user.name='RFID Operations CI' -c user.email='rfid-ops-ci@localhost' \
    tag --annotate "$tag" "$commit" --message "Release $RFID_OPS_VERSION"
fi
git push origin "refs/tags/$tag:refs/tags/$tag"
remote_object="$(git ls-remote --tags origin "refs/tags/$tag" | cut -f1)"
tag_object="$(git rev-parse "refs/tags/$tag")"
test "$remote_object" = "$tag_object"
jq -n --arg tag "$tag" --arg commit "$commit" --arg tagObject "$tag_object" \
  --arg remoteObject "$remote_object" \
  '{tag: $tag, commit: $commit, tagObject: $tagObject, remoteObject: $remoteObject,
    annotated: true, remoteVerified: true}' > "$evidence_directory/git-tag.json"
echo "Published annotated release tag $tag at $commit."
