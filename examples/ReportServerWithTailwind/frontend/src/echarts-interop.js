import * as echarts from 'echarts';

window.echarts = echarts;
export default echarts;

window.initializeECharts = () => {
  document.querySelectorAll('.chart-container').forEach(chartDom => {
    const chartId = chartDom.id;
    const data = JSON.parse(chartDom.getAttribute('data-chart'));

    if (!data) return;

    const myChart = echarts.init(chartDom);
    const option = {
      title: { text: 'ECharts Example' },
      tooltip: {},
      xAxis: { type: 'category', data: data.map(d => d.label) },
      yAxis: { type: 'value' },
      series: [{
        name: 'Value',
        type: 'bar',
        data: data.map(d => d.value),
        itemStyle: { color: '#5470C6' }
      }]
    };
    myChart.setOption(option);
  });
};
