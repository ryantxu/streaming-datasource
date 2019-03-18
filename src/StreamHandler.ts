import moment from 'moment';

import { Subject } from 'rxjs';

// See:
// https://github.com/seanlaff/simple-streaming-datasource/blob/master/src/stream_handler.js

// We return a StreamHandler wrapped in a promise from the datasource's
// Query method. Grafana expects this object to have a `subscribe` method,
// which it reads live data from.
export class StreamHandler {

  subject = new Subject();
  data:any = {
    columns: [{text:'Value'},{text:'Time'}],
    rows: [],
  };

  value :number;

  constructor(options:any, datasource:any) {

    this.value = Math.random();
    this.looper();

    console.log( 'StreamHandler', options, datasource );
  }

  subscribe = options => {
    // To avoid destroying the browser with repaints, add a throttle (You may want to tweak this)
    var throttledSubject = this.subject.pipe(); //rxjs.operators.throttleTime(100));
    return throttledSubject.subscribe(options);
  };

  looper = () => {
    this.value += (Math.random()-0.5)*.2;
    this.handleMessage( [this.value, Date.now()] );
    setTimeout(this.looper, 100);
  }

  handleMessage(row:any[]) {
    let rows = this.data.rows;

    rows.push(row);
    if(rows.length > 50) {
      rows = rows.slice(1);
      this.data.rows = rows;
    }

    const oldestTimestamp = rows[0][1];
    const mostRecentTimestamp = rows[rows.length-1][1];

    this.subject.next({
      data: [{
        datapoints: rows,
      }],
      range: { from: moment(oldestTimestamp), to: moment(mostRecentTimestamp) },
    });
  }

  // close() {
  //   if (this.reader) {
  //     this.reader.cancel('Close was called on streamHandler');
  //   }
  // }
}
