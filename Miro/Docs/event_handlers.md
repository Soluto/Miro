issueCommet -->
    when receiving merge command, check if all checks for the pr already passed (pr.status == READY). If true, merge the PR. if false, change it's status to QUEUED

pullRequest -->
    when receiving an event saying a new PR was created, insert a new document to the MergeRequests collection with status PENDING

checkSuite -->
    when receiving an event saying a check suite is completed, check if it succeded.
    If true AND pr status is QUEUED --> merge the PR.
    If true AND pr status is PENDING --> update PR status to READY
    If false, change the PR status to FAILED.
