pipeline {
  agent any
  stages {
    stage('Dependencies') {
      steps {
        sh 'nuget restore VPNShield.sln'
      }
    }
    stage('Use upstream Smod') {
        when { triggeredBy 'BuildUpstreamCause' }
        steps {
            sh ('rm VPNShield/lib/Assembly-CSharp.dll')
            sh ('rm VPNShield/lib/Smod2.dll')
            sh ('ln -s $SCPSL_LIBS/Assembly-CSharp.dll VPNShield/lib/Assembly-CSharp.dll')
            sh ('ln -s $SCPSL_LIBS/Smod2.dll VPNShield/lib/Smod2.dll')
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
        when { not { triggeredBy 'BuildUpstreamCause' } }
        steps {
            sh 'zip -r VPNShield.zip Plugin/*'
            archiveArtifacts(artifacts: 'SCPDiscord.zip', onlyIfSuccessful: true)
        }
    }
    stage('Send upstream') {
        when { triggeredBy 'BuildUpstreamCause' }
        steps {
            sh 'zip -r VPNShield.zip Plugin/*'
            sh 'cp VPNShield.zip $PLUGIN_BUILDER_ARTIFACT_DIR'
        }
    }
  }
}
