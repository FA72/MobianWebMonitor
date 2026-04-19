// Chart.js interop for Mobian Web Monitor
window.chartInterop = (() => {
    let cpuChart = null;
    let ramChart = null;
    const MAX_POINTS = 300;

    const darkTheme = {
        grid: { color: 'rgba(255,255,255,0.06)' },
        ticks: { color: '#888', font: { size: 10 } },
        borderColor: 'rgba(255,255,255,0.1)'
    };

    const coreColors = [
        '#4fc3f7', '#81c784', '#ffb74d', '#e57373',
        '#ba68c8', '#4dd0e1', '#fff176', '#a1887f'
    ];

    function formatTime(iso) {
        const d = new Date(iso);
        return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    }

    async function fetchHistory(metric, range) {
        try {
            const res = await fetch(`/api/history/${metric}?range=${range}`);
            if (!res.ok) return null;
            return await res.json();
        } catch {
            return null;
        }
    }

    return {
        initCpuChart: async function (canvasId, range, coreCount) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) return;

            if (cpuChart) { cpuChart.destroy(); cpuChart = null; }

            const datasets = [{
                label: 'Total',
                data: [],
                borderColor: '#fff',
                borderWidth: 2,
                pointRadius: 0,
                tension: 0.3,
                fill: false
            }];

            for (let i = 0; i < coreCount; i++) {
                datasets.push({
                    label: `Core ${i}`,
                    data: [],
                    borderColor: coreColors[i % coreColors.length],
                    borderWidth: 1,
                    pointRadius: 0,
                    tension: 0.3,
                    fill: false
                });
            }

            cpuChart = new Chart(canvas, {
                type: 'line',
                data: { labels: [], datasets },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: { duration: 0 },
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: {
                            display: true,
                            position: 'bottom',
                            labels: { color: '#888', boxWidth: 10, padding: 8, font: { size: 10 } }
                        }
                    },
                    scales: {
                        x: { ...darkTheme, display: true, ticks: { ...darkTheme.ticks, maxTicksLimit: 6 } },
                        y: { ...darkTheme, min: 0, max: 100, ticks: { ...darkTheme.ticks, callback: v => v + '%' } }
                    }
                }
            });

            // Load history
            const hist = await fetchHistory('cpu', range);
            if (hist && hist.series) {
                const total = hist.series['cpu.total'] || [];
                total.forEach(p => {
                    cpuChart.data.labels.push(formatTime(p.timestampUtc));
                    cpuChart.data.datasets[0].data.push(p.value);
                });
                for (let i = 0; i < coreCount; i++) {
                    const core = hist.series[`cpu.core${i}`] || [];
                    core.forEach((p, idx) => {
                        if (cpuChart.data.datasets[i + 1])
                            cpuChart.data.datasets[i + 1].data[idx] = p.value;
                    });
                }
                cpuChart.update('none');
            }
        },

        addCpuPoint: function (timeIso, total, cores) {
            if (!cpuChart) return;
            const label = formatTime(timeIso);

            cpuChart.data.labels.push(label);
            cpuChart.data.datasets[0].data.push(total);

            for (let i = 0; i < cores.length; i++) {
                if (cpuChart.data.datasets[i + 1])
                    cpuChart.data.datasets[i + 1].data.push(cores[i]);
            }

            if (cpuChart.data.labels.length > MAX_POINTS) {
                cpuChart.data.labels.shift();
                cpuChart.data.datasets.forEach(ds => ds.data.shift());
            }
            cpuChart.update('none');
        },

        initRamChart: async function (canvasId, range) {
            const canvas = document.getElementById(canvasId);
            if (!canvas) return;

            if (ramChart) { ramChart.destroy(); ramChart = null; }

            ramChart = new Chart(canvas, {
                type: 'line',
                data: {
                    labels: [],
                    datasets: [
                        {
                            label: 'Used',
                            data: [],
                            backgroundColor: 'rgba(79, 195, 247, 0.3)',
                            borderColor: '#4fc3f7',
                            borderWidth: 1.5,
                            pointRadius: 0,
                            tension: 0.3,
                            fill: true
                        },
                        {
                            label: 'Cached',
                            data: [],
                            backgroundColor: 'rgba(129, 199, 132, 0.3)',
                            borderColor: '#81c784',
                            borderWidth: 1.5,
                            pointRadius: 0,
                            tension: 0.3,
                            fill: true
                        },
                        {
                            label: 'Free',
                            data: [],
                            backgroundColor: 'rgba(255, 255, 255, 0.05)',
                            borderColor: '#555',
                            borderWidth: 1,
                            pointRadius: 0,
                            tension: 0.3,
                            fill: true
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: { duration: 0 },
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: {
                            display: true,
                            position: 'bottom',
                            labels: { color: '#888', boxWidth: 10, padding: 8, font: { size: 10 } }
                        }
                    },
                    scales: {
                        x: { ...darkTheme, display: true, stacked: true, ticks: { ...darkTheme.ticks, maxTicksLimit: 6 } },
                        y: { ...darkTheme, min: 0, max: 100, stacked: true, ticks: { ...darkTheme.ticks, callback: v => v + '%' } }
                    }
                }
            });

            const hist = await fetchHistory('ram', range);
            if (hist && hist.series) {
                const used = hist.series['mem.used_pct'] || [];
                const cached = hist.series['mem.cached_pct'] || [];
                const free = hist.series['mem.free_pct'] || [];
                used.forEach((p, i) => {
                    ramChart.data.labels.push(formatTime(p.timestampUtc));
                    ramChart.data.datasets[0].data.push(p.value);
                    ramChart.data.datasets[1].data.push(cached[i]?.value ?? 0);
                    ramChart.data.datasets[2].data.push(free[i]?.value ?? 0);
                });
                ramChart.update('none');
            }
        },

        addRamPoint: function (timeIso, used, cached, free) {
            if (!ramChart) return;
            const label = formatTime(timeIso);

            ramChart.data.labels.push(label);
            ramChart.data.datasets[0].data.push(used);
            ramChart.data.datasets[1].data.push(cached);
            ramChart.data.datasets[2].data.push(free);

            if (ramChart.data.labels.length > MAX_POINTS) {
                ramChart.data.labels.shift();
                ramChart.data.datasets.forEach(ds => ds.data.shift());
            }
            ramChart.update('none');
        }
    };
})();

// Auth interop
window.monitorAuth = {
    login: async function (password) {
        try {
            const form = new URLSearchParams();
            form.append('password', password);
            const res = await fetch('/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: form.toString()
            });
            if (res.ok) {
                return { success: true, message: null };
            }
            const text = await res.text();
            let msg = 'Access denied.';
            try {
                const j = JSON.parse(text);
                msg = j.message || msg;
            } catch { msg = text || msg; }
            return { success: false, message: msg };
        } catch (e) {
            return { success: false, message: 'Connection error.' };
        }
    },
    logout: async function () {
        await fetch('/auth/logout', { method: 'POST' });
    }
};
