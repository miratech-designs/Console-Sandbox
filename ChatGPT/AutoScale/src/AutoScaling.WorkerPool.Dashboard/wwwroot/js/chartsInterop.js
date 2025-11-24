window.charts = (function () {
    const charts = {};

    function ensureChart(ctxId, configFactory) {
        if (!charts[ctxId]) {
            const ctx = document.getElementById(ctxId).getContext('2d');
            charts[ctxId] = new Chart(ctx, configFactory());
        }
        return charts[ctxId];
    }

    function updateBacklog(id, labels, high, normal, low) {
        const cfg = () => ({
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    { label: 'High', data: high, borderColor: 'rgba(220,20,60,0.9)', fill: false },
                    { label: 'Normal', data: normal, borderColor: 'rgba(30,144,255,0.9)', fill: false },
                    { label: 'Low', data: low, borderColor: 'rgba(34,139,34,0.9)', fill: false }
                ]
            },
            options: { animation: false, responsive: true }
        });

        const c = ensureChart(id, cfg);
        c.data.labels = labels;
        c.data.datasets[0].data = high;
        c.data.datasets[1].data = normal;
        c.data.datasets[2].data = low;
        c.update();
    }

    function updateLatency(id, values) {
        const cfg = () => ({
            type: 'bar',
            data: {
                labels: values.map((_, i) => i.toString()),
                datasets: [{ label: 'Duration (ms)', data: values, backgroundColor: 'rgba(75,192,192,0.6)' }]
            },
            options: { animation: false, responsive: true }
        });

        const c = ensureChart(id, cfg);
        c.data.labels = values.map((_, i) => i.toString());
        c.data.datasets[0].data = values;
        c.update();
    }

    return { updateBacklog, updateLatency };
})();
