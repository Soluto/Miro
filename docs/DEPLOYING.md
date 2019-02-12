# Deploying Miro

<!-- TOC -->

- [Deploying Miro](#deploying-miro)
    - [Running Locally against Github](#running-locally-against-github)
    - [env vars](#env-vars)
    - [Miro Badge](#miro-badge)

<!-- /TOC -->

## Running Locally against Github

1. Create a new [Github App](https://developer.github.com/apps/building-github-apps/creating-a-github-app/)
2. Give the app the following permssions:
    - Repository administration: Read-only
    - Checks: Read-only
    - Repository contents: Read & write
    - Issues: Read & write
    - Repository metadata: Read-only
    - Pull requests: Read & write
    - Repository webhooks: Read-only
    - Commit statuses: Read & write
    - Organization hooks: Read-only
    
3. Give the app the following webhook subscriptions
    - Commit comment
    - Issue comment
    - Pull request
    - Pull request review
    - Push
    - Pull request review comment
    - Status

4. Generate a [private key](https://developer.github.com/apps/building-github-apps/authenticating-with-github-apps/#generating-a-private-key) for the github app

5. Save the private key in a private place, for example `./secrets/github-private-key.pem`    
*Note* We will be using the body of the pem:  `-----BEGIN RSA PRIVATE KEY----- <ONLY THIS PART> -----END RSA PRIVATE KEY-----`

6. Add your new app to a repo you want Miro to work on

7. Extract your app's installation id as explained [here](https://developer.github.com/apps/building-github-apps/authenticating-with-github-apps/#authenticating-as-a-github-app), we'll call this env var `GITHUB_INSTALLATION_ID`

8. In your github app's page, generate some secret key in the `Webhook secret (optional)` option.

9. Start a mongodb instance, for example `docker run --name a-mongo -p 27017:27017 -d mongo`

10. Start a tunnel (for example [ngrok](https://ngrok.com/)) on port `5000`

11. In your App general settings, change the `Webhook URL` to `https://<your-public-url>/api/webhooks/incoming/github` 

12. Pull + Run the `soluto-miro` docker image:

```sh
docker run \
-e "MONGO_CONNECTION_STRING=<your-mongo-instance-including-port>" \
-e "ASPNETCORE_ENVIRONMENT=Production" \
-e "WEBHOOKS__DISABLEHTTPSCHECK=true" \
-e "WEBHOOKS__GITHUB__SECRETKEY__DEFAULT=<your webhook secret>" \
-e "GITHUB_INSTALLATION_ID=<your-installation-id>" \
-e "GITHUB_PEM_SECRET=<your-pem-without-the-header-and-footer>" \
-p 5000:80 \
soluto-miro
```

13. Create a Pull-request in your repo, and type `miro info`


## env vars
- MONGO_CONNECTION_STRING: *Required* - Your mongo connection string
- GITHUB_PEM_SECRET: *Required* -  
- GITHUB_INSTALLATION_ID: *Required* - 
- WEBHOOKS__GITHUB__SECRETKEY__DEFAULT: *Required*
- WEBHOOKS__DISABLEHTTPSCHECK: *Required*
- API_KEY: *Optional* - If you'd like to protect the Miro API with a hard-coded api-key

## Miro Badge
Show developers this Repo's rockin Miro, put this shield at the top of your repo -

 [![Miro](https://img.shields.io/badge/Merge--Bot-Miro-green.svg)](https://github.com/Soluto/Miro)   


```md
[![Miro](https://img.shields.io/badge/Merge--Bot-Miro-green.svg)](https://github.com/Soluto/Miro)    

```