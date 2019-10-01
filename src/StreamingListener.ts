import { TimeSeriesMessage } from 'types';
import { KeyValue, CircularDataFrame, FieldType, LoadingState } from '@grafana/data';
import { Subject, Observable, ReplaySubject } from 'rxjs';
import { webSocket, WebSocketSubject } from 'rxjs/webSocket';
import { DataQueryResponse } from '@grafana/ui';

let counter = 100;

interface FrameInfo {
  key: string;
  subject: Subject<DataQueryResponse>;
  frame: CircularDataFrame;
}

export class StreamListener {
  byName: KeyValue<FrameInfo> = {};
  stream?: WebSocketSubject<any>;

  constructor(private capacity: number, url?: string) {
    if (url) {
      this.stream = webSocket({
        url,
        openObserver: {
          next: () => {
            console.log('connetion ok');
          },
        },
        closeObserver: {
          next(closeEvent) {
            console.log('connetion closed');
          },
        },
      });
      this.stream!.subscribe({
        next: (msg: any) => {
          this.process(msg as TimeSeriesMessage);
        },
      });
    } else {
      this.dummy['aaa'] = 50 + Math.random() * 25;
      this.dummy['bbb'] = 50 + Math.random() * 25;
      this.dummy['ccc'] = 50 + Math.random() * 25;
      setTimeout(this.dummyValues, 100);
    }
  }

  dummy: KeyValue<number> = {};
  dummyValues = () => {
    const time = Date.now();
    if (Math.random() > 0.3) {
      const name = 'aaa';
      const value = (this.dummy[name] = this.dummy[name] + (Math.random() - 0.5));
      this.process({ name, time, value });
    }
    if (Math.random() > 0.5) {
      const name = 'bbb';
      const value = (this.dummy[name] = this.dummy[name] + (Math.random() - 0.5));
      this.process({ name, time, value });
    }
    if (Math.random() > 0.7) {
      const name = 'ccc';
      const value = (this.dummy[name] = this.dummy[name] + (Math.random() - 0.5));
      this.process({ name, time, value });
    }
    setTimeout(this.dummyValues, 100 + Math.random() * 800); // ~1/sec
  };

  getAllObservers(): Array<Observable<DataQueryResponse>> {
    const all: Array<Observable<DataQueryResponse>> = [];
    for (const v of Object.values(this.byName)) {
      all.push(v.subject);
    }
    return all;
  }

  getOrCreate(name: string): FrameInfo {
    const info = this.byName[name];
    if (info) {
      return info;
    }
    const frame = new CircularDataFrame({ capacity: this.capacity });
    frame.name = name;
    return (this.byName[name] = {
      subject: new ReplaySubject(1),
      frame,
      key: 'S' + counter++,
    });
  }

  listen(name: string): Observable<DataQueryResponse> {
    return this.getOrCreate(name).subject;
  }

  process(msg: TimeSeriesMessage) {
    const info = this.getOrCreate(msg.name);
    const df = info.frame;
    if (!df.fields.length) {
      df.addField({ name: 'time', type: FieldType.time, config: { title: 'Time' } });
      const f = df.addFieldFor(msg.value, 'value');
      f.config.title = msg.name;
    }
    if (msg.config && df.fields[1].name === 'value') {
      const f = df.fields[1];
      f.config = { title: msg.name, ...msg.config };
    }
    if (!msg.time) {
      msg.time = Date.now();
    }
    df.values.time.add(msg.time);
    df.values.value.add(msg.value);
    df.validate();
    info.subject.next({
      key: info.key,
      state: LoadingState.Streaming, // ???
      data: [df],
    });
    console.log('PROCESS', msg);
  }
}
