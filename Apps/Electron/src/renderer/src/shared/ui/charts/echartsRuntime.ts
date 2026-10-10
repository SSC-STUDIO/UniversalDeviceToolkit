/**
 * Tree-shaken echarts registration — static imports keep Rollup able to strip
 * every chart type/component the app does not use. This module is only ever
 * reached through the dynamic import in ./echarts.ts, so the whole echarts +
 * zrender graph ("charts" chunk) stays out of the startup bundle and is
 * fetched the first time a trend chart mounts.
 *
 * Registered set: TrendChart needs line series, the cartesian grid, the axis
 * tooltip and markLine gridlines. Gauges are custom SVG (SensorGauge), so no
 * GaugeChart. SVGRenderer instead of CanvasRenderer: the trend charts are
 * low-frequency (1 Hz) simple line visuals, and SVG keeps renderer memory
 * lower than a backing canvas. If a future chart needs canvas (dense scatter,
 * large data), swap the renderer here.
 */
import { use as registerEChartsModules } from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, MarkLineComponent, TooltipComponent } from 'echarts/components'
import { SVGRenderer } from 'echarts/renderers'

registerEChartsModules([LineChart, GridComponent, TooltipComponent, MarkLineComponent, SVGRenderer])

export { init } from 'echarts/core'
