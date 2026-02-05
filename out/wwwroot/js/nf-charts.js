window.nfCharts = {
    _charts: {},

    waitTwoFrames: function () {
        return new Promise((resolve) => {
            requestAnimationFrame(() => requestAnimationFrame(resolve));
        });
    },

    // =========================
    // PIE (queso) → COLORES
    // =========================
    renderPie: function (canvasId, labels, values, colors, title, showLegend) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }

        const ctx = canvas.getContext("2d");
        const legendOn = (showLegend === true);

        this._charts[canvasId] = new Chart(ctx, {
            type: "pie",
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors,
                    borderColor: "#ffffff",
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: !legendOn,
                aspectRatio: legendOn ? undefined : 1,
                animation: { duration: 0 },
                plugins: {
                    legend: {
                        display: legendOn,
                        position: "right"
                    },
                    title: { display: false }
                }
            }
        });
    },

    // =========================
    // BAR (barras) → PALABRAS
    // =========================
    renderBar: function (canvasId, labels, values, colors, title) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }

        const ctx = canvas.getContext("2d");

        this._charts[canvasId] = new Chart(ctx, {
            type: "bar",
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: colors,
                    borderRadius: 6,
                    barPercentage: 0.5,
                    categoryPercentage: 0.6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: { duration: 0 },
                layout: { padding: { top: 8, right: 12, bottom: 8, left: 8 } },
                plugins: {
                    legend: { display: false },
                    title: { display: false }
                },
                scales: {
                    x: {
                        ticks: {
                            maxRotation: 0,
                            minRotation: 0,
                            autoSkip: false,
                            font: { size: 11 }
                        }
                    },
                    y: {
                        beginAtZero: true,
                        ticks: {
                            precision: 0,
                            stepSize: 1,
                            font: { size: 11 }
                        }
                    }
                }
            }
        });
    },

    // =========================
    // HELPERS EXPORT (OFFSCREEN)
    // =========================
    _clone: function (obj) {
        return JSON.parse(JSON.stringify(obj));
    },

    _exportPieWithLegendImage: async function (sourceCanvasId) {
        const srcCanvas = document.getElementById(sourceCanvasId);
        if (!srcCanvas) return null;

        const ch = this._charts[sourceCanvasId];
        if (!ch) return srcCanvas.toDataURL("image/png", 1.0);

        const tmp = document.createElement("canvas");

        tmp.width = 900;
        tmp.height = 420;

        const ctx = tmp.getContext("2d");

        const cfg = {
            type: "pie",
            data: this._clone(ch.data),
            options: {
                responsive: false,
                animation: { duration: 0 },
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: "right",
                        labels: {
                            boxWidth: 12,
                            boxHeight: 12,
                            padding: 14,
                            font: { size: 18 }
                        }
                    },
                    title: { display: false }
                }
            }
        };

        const tmpChart = new Chart(ctx, cfg);

        tmpChart.update("none");
        await this.waitTwoFrames();

        const img = tmp.toDataURL("image/png", 1.0);

        tmpChart.destroy();

        return img;
    },

    _exportCanvasImage: async function (canvasId) {
        const c = document.getElementById(canvasId);
        if (!c) return null;

        if (this._charts[canvasId]) this._charts[canvasId].update("none");
        await this.waitTwoFrames();

        return c.toDataURL("image/png", 1.0);
    },

    // =========================
    // EXPORTAR A PDF (2 GRÁFICOS)
    // =========================
    exportChartsPdf: async function (fileName, title, canvasId1, label1, canvasId2, label2) {
        try {
            const c1 = document.getElementById(canvasId1);
            const c2 = document.getElementById(canvasId2);

            if (!c1 || !c2) {
                alert("No se han encontrado los gráficos.");
                return;
            }

            const img1 = await this._exportPieWithLegendImage(canvasId1);

            const img2 = await this._exportCanvasImage(canvasId2);

            if (!img1 || !img2) {
                alert("No se han podido capturar los gráficos.");
                return;
            }

            const { jsPDF } = window.jspdf;
            const pdf = new jsPDF({ orientation: "p", unit: "mm", format: "a4" });

            const pageW = pdf.internal.pageSize.getWidth();
            const pageH = pdf.internal.pageSize.getHeight();
            const margin = 20;

            pdf.setFontSize(16);
            pdf.text(title || "Estadísticas", margin, 20);

            let y = 30;

            // =========================
            // COLORES (PIE) → SIN DEFORMAR
            // =========================
            pdf.setFontSize(13);
            pdf.text(label1 || "Colores", margin, y);
            y += 6;

            const contentW = pageW - margin * 2;

            // 🔑 caja bastante ancha (incluye leyenda) y con altura razonable
            const pieW = Math.min(contentW, 175);
            const pieH = 80;
            const pieX = margin + (contentW - pieW) / 2;

            pdf.addImage(img1, "PNG", pieX, y, pieW, pieH);
            y += pieH + 20;

            // =========================
            // PALABRAS (BARRAS) → MÁS ALTO Y LEGIBLE
            // =========================
            if (y + 95 > pageH) {
                pdf.addPage();
                y = 20;
            }

            pdf.setFontSize(13);
            pdf.text(label2 || "Palabras", margin, y);
            y += 6;

            const barW = pageW - margin * 2;
            const barH = 85;

            pdf.addImage(img2, "PNG", margin, y, barW, barH);

            pdf.save(fileName || "estadisticas.pdf");
        } catch (e) {
            console.error(e);
            alert("Error al generar el PDF: " + (e?.message || e));
        }
    }
};
