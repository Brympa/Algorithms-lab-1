// Chart.js Live Streaming & Multi-Series Engine for AlgorithmLab
// Strict adherence to design-taste-frontend (Obsidian, Electric Cyan, Ruby outliers, JetBrains Mono)

let activeCharts = {};

const CURATED_PALETTE = [
    { border: '#38bdf8', bg: 'rgba(56, 189, 248, 0.12)' },  // Electric Cyan
    { border: '#34d399', bg: 'rgba(52, 211, 153, 0.12)' },  // Emerald
    { border: '#fbbf24', bg: 'rgba(251, 191, 36, 0.12)' },  // Amber
    { border: '#818cf8', bg: 'rgba(129, 140, 248, 0.12)' }, // Indigo
    { border: '#fb7185', bg: 'rgba(251, 113, 133, 0.12)' }, // Rose
    { border: '#c084fc', bg: 'rgba(192, 132, 252, 0.12)' }  // Violet
];

window.initLiveChart = (canvasId, title, yAxisLabel, isStepCounting) => {
    if (typeof Chart === 'undefined') {
        console.warn('[AlgorithmLab] Chart.js is not loaded yet.');
        return;
    }

    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    if (activeCharts[canvasId]) {
        try { activeCharts[canvasId].destroy(); } catch (e) {}
    }

    try {
        activeCharts[canvasId] = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: isStepCounting ? 'Количество шагов (операций)' : 'Экспериментальные замеры',
                        data: [],
                        borderColor: '#38bdf8',
                        backgroundColor: 'rgba(56, 189, 248, 0.08)',
                        borderWidth: 2,
                        tension: 0.15,
                        pointRadius: [],
                        pointHoverRadius: 6,
                        pointBackgroundColor: [],
                        pointBorderColor: [],
                        pointStyle: []
                    },
                    {
                        label: 'Теоретическая кривая T(n)',
                        data: [],
                        borderColor: '#f59e0b',
                        borderWidth: 2,
                        borderDash: [5, 4],
                        tension: 0.15,
                        pointRadius: 0
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: {
                    title: {
                        display: !!title,
                        text: title || '',
                        color: '#e2e8f0',
                        font: { family: 'Outfit, sans-serif', size: 14, weight: 600 }
                    },
                    legend: {
                        labels: {
                            color: '#94a3b8',
                            font: { family: 'JetBrains Mono, monospace', size: 12 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(15, 23, 42, 0.95)',
                        borderColor: 'rgba(255, 255, 255, 0.15)',
                        borderWidth: 1,
                        titleFont: { family: 'Outfit, sans-serif', size: 13, weight: 600 },
                        bodyFont: { family: 'JetBrains Mono, monospace', size: 12 },
                        padding: 10,
                        displayColors: true,
                        callbacks: {
                            label: function (context) {
                                const val = context.parsed.y;
                                const isOutlier = context.dataset.pointStyle && context.dataset.pointStyle[context.dataIndex] === 'rectRot';
                                let prefix = context.dataset.label || '';
                                let formattedVal = isStepCounting ? `${val} шагов` : `${val.toFixed(5)} мс`;
                                if (isOutlier) {
                                    return `⚠ ВЫБРОС (аномалия): ${formattedVal}`;
                                }
                                return `${prefix}: ${formattedVal}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        title: { display: true, text: 'Размер входных данных (n)', color: '#64748b', font: { family: 'Outfit', size: 12 } },
                        ticks: { color: '#94a3b8', font: { family: 'JetBrains Mono', size: 11 } },
                        grid: { color: 'rgba(255, 255, 255, 0.04)' }
                    },
                    y: {
                        beginAtZero: true,
                        min: 0,
                        suggestedMin: 0,
                        title: { display: true, text: yAxisLabel || (isStepCounting ? 'Шаги' : 'Время (мс)'), color: '#64748b', font: { family: 'Outfit', size: 12 } },
                        ticks: { color: '#94a3b8', font: { family: 'JetBrains Mono', size: 11 } },
                        grid: { color: 'rgba(255, 255, 255, 0.04)' }
                    }
                }
            }
        });
    } catch (e) {
        console.warn('[AlgorithmLab] Error initializing chart:', e);
    }
};

window.appendLivePoint = (canvasId, n, val, isOutlier, theoVal) => {
    const chart = activeCharts[canvasId];
    if (!chart || !chart.data || !chart.data.datasets || chart.data.datasets.length === 0) return;

    try {
        chart.data.labels.push(n);
        const ds = chart.data.datasets[0];
        ds.data.push(val);

        if (isOutlier) {
            ds.pointRadius.push(6);
            ds.pointStyle.push('rectRot');
            ds.pointBackgroundColor.push('#f43f5e'); // Ruby Outlier
            ds.pointBorderColor.push('#ffffff');
        } else {
            ds.pointRadius.push(2.5);
            ds.pointStyle.push('circle');
            ds.pointBackgroundColor.push('#38bdf8');
            ds.pointBorderColor.push('#0284c7');
        }

        if (chart.data.datasets.length > 1 && theoVal !== undefined && theoVal !== null && theoVal > 0) {
            chart.data.datasets[1].data.push(theoVal);
        }

        chart.update('none'); // Плавный рендер без сброса холста
    } catch (e) {
        console.warn('[AlgorithmLab] Error appending live point:', e);
    }
};

window.finalizeChart = (canvasId, labels, empData, theoData, outlierIndices) => {
    const chart = activeCharts[canvasId];
    if (!chart || !chart.data || !chart.data.datasets || chart.data.datasets.length === 0) return;

    try {
        chart.data.labels = labels;
        const empDs = chart.data.datasets[0];
        empDs.data = empData;

        empDs.pointRadius = [];
        empDs.pointStyle = [];
        empDs.pointBackgroundColor = [];
        empDs.pointBorderColor = [];

        const outlierSet = new Set(outlierIndices || []);
        for (let i = 0; i < labels.length; i++) {
            if (outlierSet.has(i)) {
                empDs.pointRadius.push(7);
                empDs.pointStyle.push('rectRot');
                empDs.pointBackgroundColor.push('#f43f5e');
                empDs.pointBorderColor.push('#ffffff');
            } else {
                empDs.pointRadius.push(2.5);
                empDs.pointStyle.push('circle');
                empDs.pointBackgroundColor.push('#38bdf8');
                empDs.pointBorderColor.push('#0284c7');
            }
        }

        if (theoData && theoData.length > 0 && chart.data.datasets.length > 1) {
            chart.data.datasets[1].data = theoData;
        }

        chart.update('none');
    } catch (e) {
        console.warn('[AlgorithmLab] Error finalizing chart:', e);
    }
};

window.renderMultiSeriesChart = (canvasId, labels, seriesList, yLabel) => {
    if (typeof Chart === 'undefined') {
        console.warn('[AlgorithmLab] Chart.js is not loaded yet.');
        return;
    }

    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    if (activeCharts[canvasId]) {
        try { activeCharts[canvasId].destroy(); } catch (e) {}
    }

    try {
        const datasets = seriesList.map((s, idx) => {
            const color = CURATED_PALETTE[idx % CURATED_PALETTE.length];
            return {
                label: s.name,
                data: s.data,
                borderColor: color.border,
                backgroundColor: color.bg,
                borderWidth: 2,
                tension: 0.15,
                pointRadius: 2.5,
                spanGaps: true
            };
        });

        activeCharts[canvasId] = new Chart(ctx, {
            type: 'line',
            data: { labels: labels, datasets: datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: {
                    legend: {
                        labels: { color: '#94a3b8', font: { family: 'JetBrains Mono, monospace', size: 12 } }
                    },
                    tooltip: {
                        mode: 'index',
                        intersect: false,
                        backgroundColor: 'rgba(15, 23, 42, 0.95)',
                        borderColor: 'rgba(255, 255, 255, 0.15)',
                        borderWidth: 1,
                        titleFont: { family: 'Outfit, sans-serif' },
                        bodyFont: { family: 'JetBrains Mono, monospace' }
                    }
                },
                scales: {
                    x: {
                        title: { display: true, text: 'Размер (n)', color: '#64748b' },
                        ticks: { color: '#94a3b8', font: { family: 'JetBrains Mono' } },
                        grid: { color: 'rgba(255, 255, 255, 0.04)' }
                    },
                    y: {
                        beginAtZero: true,
                        min: 0,
                        suggestedMin: 0,
                        title: { display: true, text: yLabel || 'Время (мс)', color: '#64748b' },
                        ticks: { color: '#94a3b8', font: { family: 'JetBrains Mono' } },
                        grid: { color: 'rgba(255, 255, 255, 0.04)' }
                    }
                }
            }
        });
    } catch (e) {
        console.warn('[AlgorithmLab] Error rendering multi-series chart:', e);
    }
};

window.initMultiLiveChart = (canvasId, title, seriesNames, yAxisLabel) => {
    if (typeof Chart === 'undefined') return;
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    if (activeCharts[canvasId]) {
        try { activeCharts[canvasId].destroy(); } catch (e) {}
    }

    const datasets = seriesNames.map((name, idx) => {
        const color = CURATED_PALETTE[idx % CURATED_PALETTE.length];
        return {
            label: name,
            data: [],
            borderColor: color.border,
            backgroundColor: color.bg,
            borderWidth: 2,
            tension: 0.1,
            pointRadius: 2.5
        };
    });

    activeCharts[canvasId] = new Chart(ctx, {
        type: 'line',
        data: { labels: [], datasets: datasets },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false,
            plugins: {
                title: { display: !!title, text: title || '', color: '#e2e8f0', font: { family: 'Outfit, sans-serif', size: 14, weight: 600 } },
                legend: { labels: { color: '#94a3b8', font: { family: 'JetBrains Mono, monospace', size: 12 } } },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    backgroundColor: 'rgba(15, 23, 42, 0.95)',
                    borderColor: 'rgba(255, 255, 255, 0.15)',
                    borderWidth: 1,
                    titleFont: { family: 'Outfit, sans-serif' },
                    bodyFont: { family: 'JetBrains Mono, monospace' }
                }
            },
            scales: {
                x: {
                    title: { display: true, text: 'Степень (n)', color: '#64748b', font: { family: 'Outfit', size: 12 } },
                    ticks: { color: '#94a3b8', font: { family: 'JetBrains Mono' } },
                    grid: { color: 'rgba(255, 255, 255, 0.04)' }
                },
                y: {
                    beginAtZero: true,
                    min: 0,
                    suggestedMin: 0,
                    title: { display: true, text: yAxisLabel || 'Количество шагов (умножений)', color: '#64748b', font: { family: 'Outfit', size: 12 } },
                    ticks: { color: '#94a3b8', font: { family: 'JetBrains Mono' } },
                    grid: { color: 'rgba(255, 255, 255, 0.04)' }
                }
            }
        }
    });
};

window.appendMultiLivePoint = (canvasId, label, values) => {
    const chart = activeCharts[canvasId];
    if (!chart || !chart.data || !chart.data.datasets) return;

    try {
        chart.data.labels.push(label);
        for (let i = 0; i < values.length && i < chart.data.datasets.length; i++) {
            chart.data.datasets[i].data.push(values[i]);
        }
        chart.update('none');
    } catch (e) {
        console.warn('[AlgorithmLab] Error appending multi live point:', e);
    }
};