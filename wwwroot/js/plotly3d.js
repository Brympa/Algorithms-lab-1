// Plotly.js 3D Surface Interop for Matrix Multiplication (n x m)
window.renderPlotly3DSurface = (elementId, nValues, mValues, zMatrix) => {
    const el = document.getElementById(elementId);
    if (!el || typeof Plotly === 'undefined') return;

    const data = [{
        z: zMatrix,
        x: mValues,
        y: nValues,
        type: 'surface',
        colorscale: [
            [0, '#0ea5e9'],
            [0.5, '#6366f1'],
            [1, '#f43f5e']
        ],
        contours: {
            z: { show: true, usecolormap: true, highlightcolor: "#ffffff", project: { z: true } }
        },
        showscale: true,
        colorbar: {
            title: 'Время (мс)',
            titleside: 'right',
            tickfont: { family: 'JetBrains Mono', color: '#94a3b8' }
        }
    }];

    const layout = {
        title: {
            text: 'Зависимость времени умножения C = A × B от n и m',
            font: { family: 'Outfit, sans-serif', size: 14, color: '#f8fafc' }
        },
        autosize: true,
        margin: { l: 20, r: 20, b: 20, t: 40 },
        paper_bgcolor: 'transparent',
        plot_bgcolor: 'transparent',
        scene: {
            xaxis: {
                title: 'Размерность m',
                titlefont: { family: 'Outfit', color: '#94a3b8' },
                tickfont: { family: 'JetBrains Mono', color: '#64748b' },
                gridcolor: 'rgba(255, 255, 255, 0.05)',
                backgroundcolor: 'rgba(15, 23, 42, 0.5)'
            },
            yaxis: {
                title: 'Размерность n',
                titlefont: { family: 'Outfit', color: '#94a3b8' },
                tickfont: { family: 'JetBrains Mono', color: '#64748b' },
                gridcolor: 'rgba(255, 255, 255, 0.05)',
                backgroundcolor: 'rgba(15, 23, 42, 0.5)'
            },
            zaxis: {
                title: 'Время (мс)',
                titlefont: { family: 'Outfit', color: '#94a3b8' },
                tickfont: { family: 'JetBrains Mono', color: '#64748b' },
                gridcolor: 'rgba(255, 255, 255, 0.05)',
                backgroundcolor: 'rgba(15, 23, 42, 0.5)'
            },
            camera: {
                eye: { x: 1.5, y: -1.5, z: 1.2 }
            }
        }
    };

    const config = { responsive: true, displayModeBar: false };
    Plotly.newPlot(el, data, layout, config);
};
