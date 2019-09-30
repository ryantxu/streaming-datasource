import { DataSourcePlugin } from '@grafana/ui';
import { DataSource } from './DataSource';
import { ConfigEditor } from './ConfigEditor';
import { QueryEditor } from './QueryEditor';
import { StreamingQuery, MyDataSourceOptions } from './types';

export const plugin = new DataSourcePlugin<DataSource, StreamingQuery, MyDataSourceOptions>(DataSource)
  .setConfigEditor(ConfigEditor)
  .setQueryEditor(QueryEditor);
