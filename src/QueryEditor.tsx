import defaults from 'lodash/defaults';

import React, { PureComponent, ChangeEvent } from 'react';
import { FormField, QueryEditorProps } from '@grafana/ui';
import { DataSource } from './DataSource';
import { StreamingQuery, MyDataSourceOptions } from './types';

type Props = QueryEditorProps<DataSource, StreamingQuery, MyDataSourceOptions>;

interface State {}

export class QueryEditor extends PureComponent<Props, State> {
  onComponentDidMount() {}

  onNameChange = (event: ChangeEvent<HTMLInputElement>) => {
    const { onChange, query } = this.props;
    onChange({ ...query, name: event.target.value });
  };

  render() {
    const query = defaults(this.props.query, {});
    const name = query.name || '';

    return (
      <div className="gf-form">
        <FormField labelWidth={8} value={name} onChange={this.onNameChange} label="Name"></FormField>
      </div>
    );
  }
}
