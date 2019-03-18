import _ from "lodash";

import { StreamHandler } from './StreamHandler';

export default class StreamingDatasource {
  interval: any;

  supportsExplore: boolean = true;
  supportAnnotations: boolean = true;
  supportMetrics: boolean = true;

  openStreams: { [key: string]: StreamHandler } = {};

  /** @ngInject */
  constructor(instanceSettings, public backendSrv, public templateSrv) {
    const safeJsonData = instanceSettings.jsonData || {};

    this.interval = safeJsonData.timeInterval;
  }

  query(options:any) {
    const {panelId} = options;
    const {openStreams} = this;

    let stream = openStreams[panelId];
    if(!stream) {
      stream = new StreamHandler(options, this);
      openStreams[panelId] = stream;

      console.log( 'MAKE', openStreams );

    }

    console.log( 'GOT', stream );
    
    return Promise.resolve(stream);
  }

  queryXXX(options) {
    console.log( 'QUERY', options );
    const {range, intervalMs, maxDataPoints} = options;

    let time = range.from.valueOf();
    const stop = range.to.valueOf();

    const res:any = {
      columns: [
        {text:'Time'},
        {text:'Value'},
      ],
      rows: []
    };
    res.datapoints = res.rows;

    let count = 0;
    let value = Math.random();
    while( count++<(maxDataPoints*2) && time <stop) {
      value += (Math.random() -0.5) * 0.2;
      res.rows.push([value,time]);
      time += intervalMs;
    }

    return new Promise((resolve, reject) => {
      resolve( { data: [res] } );
    });
  }

  metricFindQuery(query: string, options?: any) {
    console.log("metricFindQuery", query, options);
    return Promise.resolve({ data: [] });
  }

  testDatasource() {
    return new Promise((resolve, reject) => {
      resolve( {
        status: "success",
        message: "yes!",
      });
    });
  }
}
