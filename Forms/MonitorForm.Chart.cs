// .NET 8 / WinForms；由已验证运行的单文件版按职责拆分。
using System;
using System.Collections.Generic;
using ModbusMonitor.Models;

namespace ModbusMonitor.Forms
{
    // 仅负责把有效采样转换成绘图数组并刷新ScottPlot。
    public partial class MonitorForm
    {
        // ---------- ScottPlot实时曲线 ----------

        private void InitializeChart()
        {
            // 图内使用英文标签，避免绘图库字体缺少中文字形。
            // 横轴是从程序启动（或最近一次切换设备）起经过的秒数。
            // 使用Stopwatch计时，不受电脑系统时间校准影响。
            temperatureChart.Plot.Title("Temperature - latest 100 samples");
            temperatureChart.Plot.XLabel("Elapsed time (s)");
            temperatureChart.Plot.YLabel("Temperature (C)");
            temperatureChart.Plot.Axes.SetLimits(0, 10, 0, 50);
        }

        private void UpdateChart()
        {
            // 本Demo只有100个点、每秒更新一次，重建一条曲线便于理解。
            // 高频/大量数据场景再学习复用数据源和批量刷新。
            // Clear只清除图中的数据对象，不会删除samples中的采样。
            temperatureChart.Plot.Clear();

            if (samples.Count == 0)
            {
                temperatureChart.Plot.Axes.SetLimits(0, 10, 0, 50);
                hasChartData = false;
                temperatureChart.Refresh();
                return;
            }

            List<double> xs = new List<double>();
            List<double> ys = new List<double>();

            for (int i = 0; i < samples.Count; i++)
            {
                TemperatureSample sample = samples[i];

                if (i > 0 && sample.BreakBefore)
                {
                    // 只在绘图数组中插入NaN作为断线标记。
                    // NaN不是0℃，也不算成功采样，不会写入samples。
                    // 用两个真实点之间的时间作为标记的位置。
                    xs.Add((samples[i - 1].ElapsedSeconds +
                        sample.ElapsedSeconds) / 2.0);
                    ys.Add(double.NaN);
                }

                xs.Add(sample.ElapsedSeconds);
                ys.Add(sample.Temperature);
            }

            // ScottPlot的Scatter默认可画点和连线。
            // X是经过的秒数，Y是温度；坐标转换和绘制交给库完成。
            var curve = temperatureChart.Plot.Add.Scatter(xs.ToArray(), ys.ToArray());
            curve.Color = ScottPlot.Colors.RoyalBlue;
            curve.LineWidth = 2;
            curve.MarkerSize = 5;

            // 首次拿到数据必须调整范围，之后由复选框控制。
            if (autoScaleCheckBox.Checked || !hasChartData)
                temperatureChart.Plot.Axes.AutoScale();

            hasChartData = true;
            temperatureChart.Refresh();
        }


    }
}
