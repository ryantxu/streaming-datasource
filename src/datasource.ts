import { DataQueryRequest, DataQueryResponse, DataSourceApi, DataSourceInstanceSettings } from '@grafana/ui';

import { StreamingQuery, MyDataSourceOptions } from './types';
import { Observable, of, merge, interval } from 'rxjs';
import { map } from 'rxjs/operators';
import { StreamListener } from 'StreamingListener';
import { LoadingState, DataFrame } from '@grafana/data';

export class DataSource extends DataSourceApi<StreamingQuery, MyDataSourceOptions> {
  listener?: StreamListener;

  constructor(instanceSettings: DataSourceInstanceSettings<MyDataSourceOptions>) {
    super(instanceSettings);
    this.listener = new StreamListener(1000, instanceSettings.url);
  }

  query(options: DataQueryRequest<StreamingQuery>): Observable<DataQueryResponse> {
    const { listener } = this;
    if (!listener) {
      throw new Error('missing listener');
    }

    const isLive = options.rangeRaw && options.rangeRaw!.to === 'now';
    if (!isLive) {
      let hasStar = false;
      let data: DataFrame[] = [];
      options.targets.forEach(t => {
        if (!t.name || t.name === '*') {
          hasStar = true;
        } else {
          data.push(listener.getOrCreate(t.name).frame);
        }
      });
      if (hasStar) {
        data = listener.getAllFrames();
      }
      return of({ data });
    }

    let hasStar = false;
    let subs: Array<Observable<DataQueryResponse>> = [];
    options.targets.forEach(t => {
      if (!t.name || t.name === '*') {
        hasStar = true;
      } else {
        subs.push(listener.listen(t.name));
      }
    });

    if (hasStar) {
      subs = listener.getAllObservers();
    }

    // Update every 1/2 sec regardless of results
    const ping = interval(1000).pipe(
      map(v => {
        return {
          key: 'heartbeat',
          state: LoadingState.Streaming,
          data: [],
        } as DataQueryResponse;
      })
    );
    subs.push(ping);

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
