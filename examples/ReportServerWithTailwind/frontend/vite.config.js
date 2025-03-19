import { defineConfig } from 'vite';

export default defineConfig({
  define: {
    'process.env.NODE_ENV': JSON.stringify('production') // or 'development'
  },
  build: {
    lib: {
      entry: 'src/echarts-interop.js',
      name: 'ECharts',
      formats: ['umd'], // Use UMD for global script usage
      fileName: () => 'echarts.js'
    },
    outDir: '../wwwroot/js', // Adjust this path as needed
    rollupOptions: {
      output: {
        globals: { echarts: "echarts" },
      },
    }
  }
});
