window.nfCharts = {
    _charts: {},

    // ✅ Espera estable (2 frames) para asegurar que el canvas está pintado
    waitTwoFrames: function () {
        return new Promise((resolve) => {
            requestAnimationFrame(() => requestAnimationFrame(resolve));
        });
    },

    // =========================
    // PIE (queso) → COLORES
    // =========================
    renderPie: function (canvasId, labels, values, colors, title) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        // destruir si ya existe
        if (this._charts[canvasId]) {
            this._charts[canvasId].destroy();
            delete this._charts[canvasId];
        }

        const ctx = canvas.getContext("2d");

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
                maintainAspectRatio: true,
                aspectRatio: 1,
                // ✅ importante para export rápido (evita que tarde en pintarse)
                animation: { duration: 0 },
                plugins: {
                    legend: { display: false },
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
                // ✅ importante para export rápido
                animation: { duration: 0 },
                plugins: {
                    legend: { display: false },
                    title: { display: false }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            precision: 0,
                            stepSize: 1
                        }
                    }
                }
            }
        });
    },

    // =========================
    // EXPORTAR A PDF (2 GRÁFICOS)
    // =========================
    exportChartsPdf: async function (fileName, title, canvasId1, label1, canvasId2, label2) {
        const c1 = document.getElementById(canvasId1);
        const c2 = document.getElementById(canvasId2);

        if (!c1 || !c2) {
            alert("No se han encontrado los gráficos.");
            return;
        }

        // fuerza render inmediato
        if (this._charts[canvasId1]) this._charts[canvasId1].update("none");
        if (this._charts[canvasId2]) this._charts[canvasId2].update("none");

        await this.waitTwoFrames();

        const img1 = c1.toDataURL("image/png", 1.0);
        const img2 = c2.toDataURL("image/png", 1.0);

        const { jsPDF } = window.jspdf;
        const pdf = new jsPDF({ orientation: "p", unit: "mm", format: "a4" });

        const pageW = pdf.internal.pageSize.getWidth();
        const pageH = pdf.internal.pageSize.getHeight();
        const margin = 20;

        // ===== TÍTULO =====
        pdf.setFontSize(16);
        pdf.text(title || "Estadísticas", margin, 20);

        let y = 30;

        // =========================
        // COLORES (PIE) → MÁS PEQUEÑO Y CENTRADO
        // =========================
        pdf.setFontSize(13);
        pdf.text(label1 || "Colores", margin, y);
        y += 6;

        const pieSize = 100; // 🔴 tamaño controlado
        const pieX = (pageW - pieSize) / 2;

        pdf.addImage(img1, "PNG", pieX, y, pieSize, pieSize);
        y += pieSize + 20;

        // =========================
        // PALABRAS (BARRAS)
        // =========================
        if (y + 80 > pageH) {
            pdf.addPage();
            y = 20;
        }

        pdf.setFontSize(13);
        pdf.text(label2 || "Palabras", margin, y);
        y += 6;

        const barWidth = pageW - margin * 2;
        const barHeight = 70;

        pdf.addImage(img2, "PNG", margin, y, barWidth, barHeight);

        pdf.save(fileName || "estadisticas.pdf");
    }

};
