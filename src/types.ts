import { DataQuery, DataSourceJsonData } from '@grafana/ui';
import { FieldConfig } from '@grafana/data';

export interface StreamingQuery extends DataQuery {
  name?: string;
}

/**
 * These are options configured for each DataSource instance
 */
export interface MyDataSourceOptions extends DataSourceJsonData {
  apiKey?: string;
}

export interface TimeSeriesMessage {
  name: string; // Name of the field
  config?: FieldConfig; // optionally include field config
  time?: number;
  value?: any;
}
