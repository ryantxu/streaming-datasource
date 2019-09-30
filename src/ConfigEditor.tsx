import React, { PureComponent, ChangeEvent } from 'react';
import { DataSourcePluginOptionsEditorProps, DataSourceSettings, FormField } from '@grafana/ui';
import { MyDataSourceOptions } from './types';

type Settings = DataSourceSettings<MyDataSourceOptions>;

interface Props extends DataSourcePluginOptionsEditorProps<Settings> {}

interface State {}

export class ConfigEditor extends PureComponent<Props, State> {
  componentDidMount() {}

  onURLChange = (event: ChangeEvent<HTMLInputElement>) => {
    const { onOptionsChange, options } = this.props;
    // const jsonData = {
    //   ...options.jsonData,
    //   apiKey: event.target.value,
    // };
    onOptionsChange({ ...options, url: event.target.value });
  };

  render() {
    const { options } = this.props;
    const { jsonData } = options;

    return (
      <div className="gf-form-group">
        <div className="gf-form">
          <FormField label="Web Socket URL" labelWidth={10} onChange={this.onURLChange} value={jsonData.apiKey || ''} placeholder="Websocket URL" />
        </div>
      </div>
    );
  }
}
