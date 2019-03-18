import moment from 'moment';

import { Subject } from 'rxjs';
import _ from 'lodash';

export interface StreamHandlerOptions {
  delay: number;
  maxPoints: number;
}

const defaultOptions: StreamHandlerOptions = {
  delay: 100, // 10hz
  maxPoints: 50, //
};

export class StreamHandler extends Subject<any> {
  data: any = {
    columns: [{ text: 'Value' }, { text: 'Time' }],
    rows: [],
  };

  options: StreamHandlerOptions;

  constructor(options: any, datasource: any) {
    super();

    this.options = _.defaults(options, defaultOptions);
  }

  addRows(add: any[][]) {
    let rows = this.data.rows;

    // Add each row
    add.forEach(row => {
      rows.push(row);
    });

    if (rows.length > this.options.maxPoints) {
      rows = rows.slice(1);
      this.data.rows = rows;
    }

    const oldestTimestamp = rows[0][1];
    const mostRecentTimestamp = rows[rows.length - 1][1];

    this.next({
      data: [
        {
          datapoints: rows,
        },
      ],
      range: { from: moment(oldestTimestamp), to: moment(mostRecentTimestamp) },
    });
  }

  error(err: any): void {
    super.error(err);
    console.log('GOT AN ERROR!', err, this);
  }

  complete(): void {
    super.complete();
    console.log('COMPLETE', this);
  }
}
