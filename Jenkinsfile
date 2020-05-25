pipeline {
  agent any
  stages {
    stage('Dependencies') {
      steps {
        sh 'nuget restore VPNShield.sln'
      }
    }
    stage('Build') {
      steps {
        sh 'msbuild VPNShield/VPNShield.csproj -restore -p:PostBuildEvent='
      }
    }
    stage('Setup Output Dir') {
      steps {
        sh 'mkdir Plugin'
        sh 'mkdir Plugin/dependencies'
      }
    }
    stage('Package') {
      steps {
        sh 'mv VPNShield/bin/VPNShield.dll Plugin/'
        sh 'mv VPNShield/bin/Newtonsoft.Json.dll Plugin/dependencies'
      }
    }
    stage('Archive') {
      steps {
        sh 'zip -r VPNShield.zip Plugin'
        archiveArtifacts(artifacts: 'VPNShield.zip', onlyIfSuccessful: true)
      }
    }
  }
}
