## Grafana Streaming Datasource (WORK IN PROGRESS)

[![CircleCI](https://circleci.com/gh/ryantxu/streaming-datasource/tree/master.svg?style=svg)](https://circleci.com/gh/ryantxu/streaming-datasource/tree/master)
[![dependencies Status](https://david-dm.org/ryantxu/streaming-datasource/status.svg)](https://david-dm.org/ryantxu/streaming-datasource)
[![devDependencies Status](https://david-dm.org/ryantxu/streaming-datasource/dev-status.svg)](https://david-dm.org/ryantxu/streaming-datasource?type=dev)

Simple streaming datasource.  See also:
https://github.com/seanlaff/simple-streaming-datasource/


This currently does a javascript only random walk


### Example Server

To run the sample server run:
```
go run server.go
```


To see the output it produces, run:
```
curl --no-buffer http://localhost:7777
```


### Building

To complie, run:

```
yarn install --pure-lockfile
yarn build
```

#### Changelog

##### v0.0.1-dev

* First working version
