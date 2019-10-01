import { DataQueryRequest, DataQueryResponse, DataSourceApi, DataSourceInstanceSettings } from '@grafana/ui';

import { StreamingQuery, MyDataSourceOptions } from './types';
import { Observable, of, merge } from 'rxjs';
import { StreamListener } from 'StreamingListener';

export class DataSource extends DataSourceApi<StreamingQuery, MyDataSourceOptions> {
  listener?: StreamListener;

  constructor(instanceSettings: DataSourceInstanceSettings<MyDataSourceOptions>) {
    super(instanceSettings);
    this.listener = new StreamListener(1000, instanceSettings.url);
  }

  query(options: DataQueryRequest<StreamingQuery>): Observable<DataQueryResponse> {
    if (!this.listener) {
      throw new Error('missing listener');
    }

    let hasStar = false;
    let subs: Array<Observable<DataQueryResponse>> = [];
    options.targets.forEach(t => {
      if (!t.name || t.name === '*') {
        hasStar = true;
      } else {
        subs.push(this.listener!.listen(t.name));
      }
    });

    if (hasStar) {
      subs = this.listener.getAllObservers();
    }
    if (subs.length === 0) {
      return of({ data: [] }); // nothing
    }
    if (subs.length === 1) {
      return subs[0];
    }
    return merge(...subs);
  }

  testDatasource() {
    // Implement a health check for your data source.
    return new Promise((resolve, reject) => {
      resolve({
        status: 'success',
        message: 'Success',
      });
    });
  }
}
