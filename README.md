## Grafana Flat System Datasource (WORK IN PROGRESS)

[![CircleCI](https://circleci.com/gh/ryantxu/file-system-datasource/tree/master.svg?style=svg)](https://circleci.com/gh/ryantxu/file-system-datasource/tree/master)
[![dependencies Status](https://david-dm.org/ryantxu/file-system-datasource/status.svg)](https://david-dm.org/ryantxu/file-system-datasource)
[![devDependencies Status](https://david-dm.org/ryantxu/file-system-datasource/dev-status.svg)](https://david-dm.org/ryantxu/file-system-datasource?type=dev)

Given a simple file system backend this datasource will:
 * list files w/metadata
 * expose some formats as timeseries/table data. Specifficaly:
    * csv
    * avro


### Building

To complie, run:

```
yarn install --pure-lockfile
yarn build
```

#### Changelog

##### v0.0.1-dev

* First working version
