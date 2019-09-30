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
  stream: WebSocketSubject<any>;

  constructor(private capacity: number, url: string) {
    this.stream = webSocket(url);
    this.stream.subscribe({
      next: (msg: any) => {
        console.log('GOT', msg);
      },
    });
  }

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
  }
}
