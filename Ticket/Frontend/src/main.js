import Alpine from 'alpinejs';
import * as lucide from 'lucide-static';
import './css/index.css';

window.Alpine = Alpine;

// Initialize Alpine
Alpine.start();

window.ticketDetailsPage = (config) => ({
    ticketId: config.ticketId,
    rowVersion: config.rowVersion,
    showStatusModal: false,
    newStatus: '',
    statusNote: '',
    commentBody: '',
    actor: {
        name: '',
        email: '',
        actorType: 'Requester'
    },
    ensureActor() {
        if (!this.actor.email || !this.actor.name) {
            alert('Please provide your name and email so we can authorize the action.');
            return false;
        }
        return true;
    },
    async updateStatus() {
        if (!this.newStatus) {
            return;
        }

        if (!this.ensureActor()) {
            return;
        }

        try {
            const response = await fetch(`/tickets/${this.ticketId}/status`, {
                method: 'PATCH',
                headers: {
                    'Content-Type': 'application/json',
                    'If-Match': `"${this.rowVersion}"`
                },
                body: JSON.stringify({
                    status: this.newStatus,
                    note: this.statusNote,
                    changedBy: this.actor.name,
                    actor: this.actor
                })
            });

            if (response.ok) {
                window.location.reload();
            } else {
                const payload = await response.json().catch(() => ({}));
                alert(payload.detail ?? 'Failed to update status.');
            }
        } catch (error) {
            console.error(error);
            alert('Failed to update status.');
        }
    },
    async submitComment() {
        if (!this.commentBody.trim()) {
            alert('Comment cannot be empty.');
            return;
        }

        if (!this.ensureActor()) {
            return;
        }

        try {
            const response = await fetch(`/tickets/${this.ticketId}/comments`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    body: this.commentBody,
                    actor: this.actor
                })
            });

            if (response.ok) {
                this.commentBody = '';
                window.location.reload();
            } else {
                const payload = await response.json().catch(() => ({}));
                alert(payload.detail ?? 'Failed to add comment.');
            }
        } catch (error) {
            console.error(error);
            alert('Failed to add comment.');
        }
    }
});
