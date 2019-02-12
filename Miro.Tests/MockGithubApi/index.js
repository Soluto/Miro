const express = require("express");
const bodyParser = require("body-parser");
const app = express();
const FakeServer = require("simple-fake-server").FakeServer;
const uuidv4 = require("uuid/v4");
const escapeStringRegexp = require("escape-string-regexp");

fakeServer = new FakeServer(1234);
fakeServer.start();

let mockedCalls = {};

app.use(bodyParser.json());

app.post("/fake_server_admin/calls", (req, res) => {
  const mockedMethod = req.body.method;
  const mockedUrl = req.body.url;
  const mockedReqBody = req.body.body;
  const mockedResponse = req.body.response;
  const isJson = req.body.isJson;
  const statusCode = req.body.statusCode;

  console.log(
    `Simple-Fake-Server got mock call to ${mockedMethod} ${mockedUrl} \n mocked Body : ${mockedReqBody}, mockedStatus: ${statusCode}`
  )
  let callId = uuidv4();
  let call;
  if (statusCode && statusCode !== 200) {
    call = fakeServer.http[mockedMethod]()
      .to(mockedUrl)
      .withBodyThatMatches(mockedReqBody ? `.*[${escapeStringRegexp(mockedReqBody)}].*` : '.*')
      .willFail(statusCode);
  } else {
    call = fakeServer.http[mockedMethod]()
      .to(mockedUrl)
      .withBodyThatMatches(mockedReqBody ? `.*[${escapeStringRegexp(mockedReqBody)}].*` : '.*')
      .willReturn(isJson ? JSON.parse(mockedResponse) : mockedResponse);
  }
  mockedCalls[callId] = call;
    res.send({ callId });
});

app.delete("/fake_server_admin/calls", (req, res) => {
  console.log(
    'Got a request to clear all mocks'
  )
  fakeServer.callHistory.clear();
  res.send("Ok");
});

app.get("/fake_server_admin/calls", (req, res) => {
  const callId = req.query.callId;

  let hasBeenMade;

  const mockedCall = mockedCalls[callId] || { call: {} };
  const madeCall = fakeServer.callHistory.calls.filter(
    c =>
      c.method === mockedCall.call.method &&
      new RegExp(mockedCall.call.pathRegex).test(c.path)
  )[0];

  if (!mockedCall) {
    res.send({ hasBeenMade: false });
  } else if (!madeCall) {
    res.send({ hasBeenMade: false });
  } else {
    res.send({ hasBeenMade: true, details: madeCall });
  }
});

app.listen(3000, () =>
  console.log(
    "simple fake server admin is on port 3000, mocked api is on port 1234"
  )
);
