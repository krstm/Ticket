import Alpine from 'alpinejs';
import Chart from 'chart.js/auto';
import DOMPurify from 'dompurify';
import './css/index.css';

window.Alpine = Alpine;

// Initialize Alpine
Alpine.start();

const bucketLabels = (entries = []) => entries.map((entry) => entry?.Bucket ?? entry?.bucket ?? '');
const bucketCounts = (entries = []) => entries.map((entry) => Number(entry?.Count ?? entry?.count ?? 0));

const sanitizeInput = (value) => DOMPurify.sanitize(value ?? '', { ALLOWED_TAGS: [], ALLOWED_ATTR: [] }).trim();

const readJsonSafely = async (response) => {
    try {
        return await response.clone().json();
    } catch {
        return null;
    }
};

const decodeBase64Json = (encodedValue) => {
    if (!encodedValue) {
        return [];
    }

    try {
        const decoded = typeof atob === 'function' ? atob(encodedValue) : encodedValue;
        const parsed = JSON.parse(decoded);
        return Array.isArray(parsed) ? parsed : [];
    } catch (error) {
        console.error('Failed to parse dashboard payload', error);
        return [];
    }
};

const createTrendChart = (ctx, entries = []) => {
    if (!ctx) {
        return null;
    }

    return new Chart(ctx, {
        type: 'line',
        data: {
            labels: bucketLabels(entries),
            datasets: [{
                label: 'Requests',
                data: bucketCounts(entries),
                borderColor: '#3b68f6',
                backgroundColor: 'rgba(59, 104, 246, 0.1)',
                fill: true,
                tension: 0.4,
                borderWidth: 3,
                pointRadius: 4,
                pointBackgroundColor: '#ffffff',
                pointBorderColor: '#3b68f6',
                pointBorderWidth: 2,
            }],
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
            },
            scales: {
                y: {
                    beginAtZero: true,
                    grid: { borderDash: [5, 5] },
                    ticks: { font: { family: 'Inter' } },
                },
                x: {
                    grid: { display: false },
                    ticks: { font: { family: 'Inter' } },
                },
            },
        },
    });
};

const createStatusChart = (ctx, entries = []) => {
    if (!ctx) {
        return null;
    }

    return new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: bucketLabels(entries),
            datasets: [{
                data: bucketCounts(entries),
                backgroundColor: ['#3b68f6', '#10b981', '#f59e0b', '#64748b', '#ef4444'],
                borderWidth: 0,
                hoverOffset: 10,
            }],
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: '70%',
            plugins: {
                legend: {
                    position: 'right',
                    labels: {
                        boxWidth: 10,
                        padding: 20,
                        font: { family: 'Inter', weight: 'bold' },
                    },
                },
            },
        },
    });
};

window.initReportsDashboard = ({ trendData = [], summaryData = [], trendCanvas, summaryCanvas } = {}) => {
    const charts = [];
    const trendContext = trendCanvas instanceof HTMLCanvasElement ? trendCanvas.getContext('2d') : null;
    const summaryContext = summaryCanvas instanceof HTMLCanvasElement ? summaryCanvas.getContext('2d') : null;

    if (trendContext) {
        const chart = createTrendChart(trendContext, Array.isArray(trendData) ? trendData : []);
        if (chart) {
            charts.push(chart);
        }
    }

    if (summaryContext) {
        const chart = createStatusChart(summaryContext, Array.isArray(summaryData) ? summaryData : []);
        if (chart) {
            charts.push(chart);
        }
    }

    return charts;
};

document.addEventListener('DOMContentLoaded', () => {
    const dashboards = document.querySelectorAll('[data-reports-dashboard]');
    dashboards.forEach((container) => {
        window.initReportsDashboard({
            trendData: decodeBase64Json(container.getAttribute('data-trend-b64')),
            summaryData: decodeBase64Json(container.getAttribute('data-summary-b64')),
            trendCanvas: container.querySelector('#trendChart'),
            summaryCanvas: container.querySelector('#statusChart'),
        });
    });
});

const allowedActorTypes = new Set(['Requester', 'DepartmentMember']);

const ticketActorState = Alpine.reactive({
    name: '',
    email: '',
    actorType: 'Requester',
});

window.ticketActorContext = {
    state: ticketActorState,
    snapshot() {
        const actorType = allowedActorTypes.has(ticketActorState.actorType)
            ? ticketActorState.actorType
            : 'Requester';

        return {
            name: sanitizeInput(ticketActorState.name),
            email: sanitizeInput(ticketActorState.email),
            actorType,
        };
    },
    ensure() {
        const actor = this.snapshot();
        if (!actor.name || !actor.email) {
            alert('Please provide your name and email so we can authorize the action.');
            return null;
        }
        return actor;
    },
};

window.sanitizeInput = sanitizeInput;

window.ticketDetailsPage = (config) => ({
    ticketId: config.ticketId,
    rowVersion: config.rowVersion,
    showStatusModal: false,
    newStatus: '',
    statusNote: '',
    commentBody: '',
    actor: window.ticketActorContext.state,
    ensureActor() {
        return window.ticketActorContext.ensure();
    },
    async updateStatus() {
        if (!this.newStatus) {
            return;
        }

        const actor = this.ensureActor();
        if (!actor) {
            return;
        }

        const note = sanitizeInput(this.statusNote);

        try {
            const response = await fetch(`/tickets/${this.ticketId}/status`, {
                method: 'PATCH',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest',
                    'If-Match': `"${this.rowVersion}"`
                },
                body: JSON.stringify({
                    status: this.newStatus,
                    note,
                    changedBy: actor.name,
                    actor
                })
            });

            if (response.ok) {
                window.location.reload();
                return;
            }

            const payload = await readJsonSafely(response);
            alert(payload?.detail ?? 'Failed to update status.');
        } catch (error) {
            console.error(error);
            alert('Failed to update status.');
        }
    },
    async submitComment() {
        const sanitizedComment = sanitizeInput(this.commentBody);
        if (!sanitizedComment) {
            alert('Comment cannot be empty.');
            return;
        }

        const actor = this.ensureActor();
        if (!actor) {
            return;
        }

        try {
            const response = await fetch(`/tickets/${this.ticketId}/comments`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({
                    body: sanitizedComment,
                    actor
                })
            });

            if (response.ok) {
                this.commentBody = '';
                window.location.reload();
                return;
            }

            const payload = await readJsonSafely(response);
            alert(payload?.detail ?? 'Failed to add comment.');
        } catch (error) {
            console.error(error);
            alert('Failed to add comment.');
        }
    }
});
