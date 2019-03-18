import _ from 'lodash';
import { StreamHandler } from './StreamHandler';

export class RandomWalkStream extends StreamHandler {
  value: number;

  constructor(options: any, datasource: any) {
    super(options, datasource);

    this.value = Math.random();
    this.data.rows = this.fillBuffer(options);
    this.looper();
  }

  fillBuffer(options): any[][] {
    console.log('QUERY', options);
    const { range, intervalMs, maxDataPoints } = options;

    let time = range.from.valueOf();
    const stop = range.to.valueOf();

    const rows: any[][] = [];

    let count = 0;
    while (count++ < maxDataPoints * 2 && time < stop) {
      this.value += (Math.random() - 0.5) * 0.2;
      rows.push([this.value, time]);
      time += intervalMs;
    }
    return rows;
  }

  looper = () => {
    this.value += (Math.random() - 0.5) * 0.2;
    this.addRows([[this.value, Date.now()]]);
    setTimeout(this.looper, this.options.delay);
  };
}
