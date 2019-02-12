# Contributing to MIRO

<!-- TOsC -->

- [Contributing to MIRO](#contributing-to-miro)
    - [Set up environment](#set-up-environment)
    - [Testing](#testing)
    - [Testing Locally](#testing-locally)

<!-- /TOC -->

## Set up environment
1. install .net core sdk
2. if working with vs code --> install c# extension

## Testing

Run the test suite
1. `cd Miro.Tests && docker-compose up --build --abort-on-container-exit`

If you want to run the tests separately without re-building the app
1. `cd Miro.Tests`
2. `docker-compose build` - Will Build the bot, GithubApi, MongoDb
3. `docker-compose run --rm --name miro-tests miro-tests` - Will Run the tests

To run a single test, replace step (3) with this:
1. `docker-compose run --rm --name miro-tests --entrypoint 'dotnet test --filter "Miro.Tests.<TEST-CLASS>.<TEST-NAME>"' miro-tests`

## Testing Locally
If you want to run the tests without docker
1. `cd Miro.Tests`
2. `docker-compose build`
3. `docker-compose run --rm --service-ports --name miro-app miro-app`
4. `dotnet restore && dotnet test`


