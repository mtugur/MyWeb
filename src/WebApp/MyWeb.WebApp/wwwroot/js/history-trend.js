(() => {
    const $ = (sel) => document.querySelector(sel);

    let chart;

    function buildChart(ctx) {
        if (chart) chart.destroy();

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                datasets: []
            },
            options: {
                parsing: false,
                animation: false,
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        type: 'time',
                        time: {
                            displayFormats: {
                                minute: 'dd.MM HH:mm',
                                hour: 'dd.MM HH:mm',
                                day: 'dd.MM',
                                week: 'dd.MM.yyyy'
                            },
                            tooltipFormat: 'dd.MM.yyyy HH:mm:ss'
                        },
                        ticks: {
                            autoSkip: true,
                            maxRotation: 0
                        }
                    },
                    y: {
                        beginAtZero: false
                    }
                },
                plugins: {
                    legend: { display: true }
                }
            }
        });
    }

    async function fetchSamples(tagId, fromIsoUtc, toIsoUtc, take) {
        const url = `/api/hist/samples?tagId=${tagId}&from=${encodeURIComponent(fromIsoUtc)}&to=${encodeURIComponent(toIsoUtc)}&take=${take ?? 5000}`;
        try {
            const res = await fetch(url);
            if (!res.ok) throw new Error(`HTTP ${res.status}: ${await res.text()}`);
            const arr = await res.json();

            const points = arr
                .filter(r => r.valueNumeric !== null && r.valueNumeric !== undefined)
                .map(r => ({
                    x: new Date(r.utc),
                    y: r.valueNumeric
                }));

            console.log('Fetched points (UTC):', points);
            return points;
        } catch (ex) {
            console.error('Fetch samples failed:', ex);
            throw ex;
        }
    }

    async function fetchTags(projectId) {
        const url = `/api/hist/tags?projectId=${projectId}`;
        try {
            const res = await fetch(url);
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return await res.json();
        } catch (ex) {
            console.error('Fetch tags failed:', ex);
            throw ex;
        }
    }

    async function updateTags() {
        const projectId = $('#projectSelect').value;
        const tagSelect = $('#tagSelect');
        try {
            const tags = await fetchTags(projectId);
            tagSelect.innerHTML = tags.map(t => `<option value="${t.id}">${t.path}</option>`).join('');
            if (tags.length === 0) {
                tagSelect.innerHTML = '<option value="">Tag bulunamadı</option>';
            }
        } catch (ex) {
            tagSelect.innerHTML = '<option value="">Tag yüklenemedi</option>';
            showError('Tag listesi yüklenemedi: ' + ex.message);
        }
    }

    function localInputToIsoUtc(el) {
        const d = new Date(el.value);
        const offsetMs = d.getTimezoneOffset() * 60000;
        return new Date(d.getTime() + offsetMs).toISOString(); // Değişiklik: - yerine + , kaymayı düzeltmek için
    }

    function showError(message) {
        const alertDiv = document.createElement('div');
        alertDiv.className = 'alert alert-warning mt-3';
        alertDiv.textContent = message;
        $('#trendWrap').prepend(alertDiv);
        setTimeout(() => alertDiv.remove(), 5000);
    }

    async function onGetir() {
        const tagId = $('#tagSelect').value;
        const fromInput = $('#fromInput');
        const toInput = $('#toInput');
        const fromIso = localInputToIsoUtc(fromInput);
        const toIso = localInputToIsoUtc(toInput);

        if (!fromInput.value || !toInput.value) {
            showError('Başlangıç ve bitiş tarihlerini seçin.');
            return;
        }
        if (new Date(fromIso) >= new Date(toIso)) {
            showError('Bitiş tarihi başlangıç tarihinden sonra olmalıdır.');
            return;
        }
        if (!tagId) {
            showError('Lütfen bir tag seçin.');
            return;
        }

        try {
            const pts = await fetchSamples(tagId, fromIso, toIso, 5000);
            chart.data.datasets = [{
                label: $('#tagSelect option:checked').text,
                data: pts,
                borderWidth: 2,
                pointRadius: 0,
                tension: 0.15,
                borderColor: '#36A2EB',
                backgroundColor: 'rgba(54, 162, 235, 0.2)'
            }];
            chart.options.scales.x.min = fromIso;
            chart.options.scales.x.max = toIso;
            chart.update();
            console.log('Chart updated with UTC range:', { from: fromIso, to: toIso });
        } catch (ex) {
            showError('Veri yüklenemedi: ' + ex.message);
        }
    }

    window.HISTORY_TREND_BOOT = (config) => {
        const ctx = document.getElementById('trendChart').getContext('2d');
        buildChart(ctx);

        $('#projectSelect').addEventListener('change', updateTags);
        $('#btnFetch').addEventListener('click', onGetir);

        const to = new Date();
        const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
        const toLocalInput = (d) => new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
        $('#fromInput').value = toLocalInput(from);
        $('#toInput').value = toLocalInput(to);

        updateTags();
    };
})();