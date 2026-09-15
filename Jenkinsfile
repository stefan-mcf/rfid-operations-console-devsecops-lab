pipeline {
    agent any

    options {
        buildDiscarder(logRotator(numToKeepStr: '12'))
        disableConcurrentBuilds()
        skipDefaultCheckout(true)
        timeout(time: 45, unit: 'MINUTES')
    }

    environment {
        RFID_OPS_IMAGE_REPOSITORY = 'rfid-ops'
        RFID_OPS_HOST_ADDRESS = 'host.docker.internal'
        SONAR_HOST_URL = 'http://sonarqube:9000'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
    }

    stages {
        stage('Checkout') {
            steps {
                script {
                    def checkoutResult = checkout scm
                    env.RFID_OPS_VERSION = "1.0.${env.BUILD_NUMBER}-${checkoutResult.GIT_COMMIT.take(7)}"
                }
                dir('artifacts') {
                    deleteDir()
                }
                sh 'mkdir -p artifacts'
            }
        }

        stage('Build') {
            steps {
                sh 'scripts/ci/build.sh'
            }
        }

        stage('Test') {
            steps {
                sh 'scripts/ci/test.sh'
            }
            post {
                always {
                    junit allowEmptyResults: true,
                        testResults: 'artifacts/test-results/*-test-result.xml'
                }
            }
        }

        stage('Code Quality') {
            environment {
                SONAR_TOKEN = credentials('sonar-token')
            }
            steps {
                sh 'scripts/ci/quality.sh'
            }
        }

        stage('Security') {
            steps {
                sh 'scripts/ci/security.sh'
            }
        }

        stage('Deploy') {
            environment {
                RFID_OPS_READER_KEY = credentials('rfid-ops-reader-key')
                RFID_OPS_ADMIN_KEY = credentials('rfid-ops-admin-key')
            }
            steps {
                sh 'scripts/ci/deploy.sh'
            }
        }

        stage('Release') {
            environment {
                RFID_OPS_READER_KEY = credentials('rfid-ops-reader-key')
                RFID_OPS_ADMIN_KEY = credentials('rfid-ops-admin-key')
            }
            steps {
                sh 'scripts/ci/release.sh'
            }
        }

        stage('Monitoring and Alerting') {
            environment {
                RFID_OPS_READER_KEY = credentials('rfid-ops-reader-key')
                RFID_OPS_ADMIN_KEY = credentials('rfid-ops-admin-key')
                GRAFANA_ADMIN_PASSWORD = credentials('grafana-admin-password')
            }
            steps {
                sh 'scripts/ci/monitor.sh'
            }
        }
    }

    post {
        always {
            archiveArtifacts allowEmptyArchive: true,
                artifacts: 'artifacts/**/*',
                fingerprint: true
        }
        cleanup {
            sh '''
                RFID_OPS_IMAGE="${RFID_OPS_IMAGE_REPOSITORY}:${RFID_OPS_VERSION:-cleanup}" docker compose \
                  --project-name rfid-ops-staging \
                  --file deploy/compose.staging.yml \
                  down || true
            '''
        }
    }
}
