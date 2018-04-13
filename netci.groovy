// Import the utility functionality.

import jobs.generation.Utilities;

// Defines a the new of the repo, used elsewhere in the file

def project = GithubProject
def branch = GithubBranchName

// Generate the builds for debug and release, commit and PRJob
[true, false].each { isPR -> // Defines a closure over true and false, value assigned to isPR
    ['Debug', 'Release'].each { configuration ->
  
        // Determine the name for the new job. A _prtest suffix is appended if isPR is true.
        def newJobName = Utilities.getFullJobName(project, configuration, isPR)    

        // Define your build/test strings here
        def buildString = """call build.cmd -c ${configuration} -full -clean"""
        def testString = """call test.cmd -c ${configuration} -parallel"""
        def smokeTestString = """call test.cmd -c ${configuration} -p smoke"""
        
        // Create a new job for windows build
        def newJob = job(newJobName) {
            steps {
                batchFile(buildString)
                batchFile(testString)
                batchFile(smokeTestString)
            }
        }

         // Move to latest VS(15.6) machines
        Utilities.setMachineAffinity(newJob, 'Windows_NT', 'Windows.10.Amd64.ClientRS3.DevEx.Open')

        // Archive trx files for logs
        Utilities.addArchival(newJob, '**/TestResults/**/*.trx', '', true, false)

        // This call performs remaining common job setup on the newly created job.
        Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")

        // Specifying save duration for logs
        newJob.with {
          logRotator {
             artifactDaysToKeep(30)
             daysToKeep(30)
             artifactNumToKeep(200)
             numToKeep(200)
        }

        if (isPR) {
            Utilities.addGithubPRTriggerForBranch(newJob, branch, "Windows / ${configuration} Build")
        }
        else {
            Utilities.addGithubPushTrigger(newJob)
        }
    }
}