// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Timezone conversion utilities
const TimezoneUtil = {
    /**
     * Converts a UTC ISO string to local time and formats it
     * @param {string} utcIsoString - UTC datetime string (ISO format)
     * @param {string} format - Format type: 'short', 'long', 'time', 'datetime'
     * @returns {string} Formatted local time string
     */
    formatUtcToLocal: function(utcIsoString, format = 'datetime') {
        if (!utcIsoString) return '';
        
        const date = new Date(utcIsoString);
        if (isNaN(date)) return utcIsoString;
        
        const options = {
            short: { year: 'numeric', month: 'short', day: 'numeric' },
            long: { year: 'numeric', month: 'long', day: 'numeric', weekday: 'long' },
            time: { hour: 'numeric', minute: '2-digit', second: '2-digit' },
            datetime: { year: 'numeric', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }
        };
        
        return date.toLocaleString(undefined, options[format] || options.datetime);
    },

    /**
     * Converts UTC datetime to local time for display in elements with data-utc attribute
     */
    convertAllTimestamps: function() {
        // Find all elements with data-utc attribute
        document.querySelectorAll('[data-utc]').forEach(element => {
            const utcString = element.getAttribute('data-utc');
            const format = element.getAttribute('data-format') || 'datetime';
            const localTime = this.formatUtcToLocal(utcString, format);
            element.textContent = localTime;
        });
    },

    /**
     * Initialize real-time countdown timer
     * @param {string} elementId - ID of element to update
     * @param {string} expiresAtUtc - Expiration time in UTC (ISO format)
     */
    startCountdown: function(elementId, expiresAtUtc) {
        const element = document.getElementById(elementId);
        if (!element) return;
        
        const updateTimer = () => {
            const now = new Date();
            const expiresAt = new Date(expiresAtUtc);
            const diff = expiresAt - now;
            
            if (diff <= 0) {
                element.textContent = 'Expired';
                element.classList.remove('text-info');
                element.classList.add('text-danger');
                return;
            }
            
            const hours = Math.floor(diff / 3600000);
            const minutes = Math.floor((diff % 3600000) / 60000);
            const seconds = Math.floor((diff % 60000) / 1000);
            
            element.textContent = `${hours}h ${minutes}m ${seconds}s`;
        };
        
        // Update immediately
        updateTimer();
        
        // Update every second
        setInterval(updateTimer, 1000);
    }
};

// Convert timestamps when page loads
document.addEventListener('DOMContentLoaded', function() {
    TimezoneUtil.convertAllTimestamps();
});
