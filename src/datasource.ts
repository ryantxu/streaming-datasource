import _ from 'lodash';

import { RandomWalkStream } from './RandomWalkStream';
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

  query(options: any) {
    const { panelId } = options;
    const { openStreams } = this;

    let stream = openStreams[panelId];
    if (!stream) {
      stream = new RandomWalkStream(options, this);
      openStreams[panelId] = stream;
      console.log('MAKE', openStreams);
    }
    return Promise.resolve(stream);
  }

  metricFindQuery(query: string, options?: any) {
    console.log('metricFindQuery', query, options);
    return Promise.resolve({ data: [] });
  }

  testDatasource() {
    return new Promise((resolve, reject) => {
      resolve({
        status: 'success',
        message: 'yes!',
      });
    });
  }
}
