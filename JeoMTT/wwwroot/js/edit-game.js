/**
 * Edit Game Form Handler
 * Tracks form interactions and user activity
 */

let formModified = false;

/**
 * Track when form changes
 */
function trackFormChange() {
    formModified = true;
}

/**
 * Track when user starts editing
 */
function trackFormStart(fieldName) {
    if (window.trackEvent && !formModified) {
        const gameIdInput = document.querySelector('[name="Id"]');
        
        trackEvent('JeoGameEditFormStarted', {
            'gameId': gameIdInput ? gameIdInput.value : 'unknown',
            'field': fieldName
        });
    }
}

/**
 * Track form submission before submit
 */
function trackBeforeSubmit(event) {
    const button = event.submitter;
    const buttonValue = button ? button.value : 'unknown';
    
    if (window.trackEvent) {
        const gameNameInput = document.querySelector('[name="Name"]');
        const gameIdInput = document.querySelector('[name="Id"]');
        
        trackEvent('JeoGameEditFormSubmitted', {
            'gameId': gameIdInput ? gameIdInput.value : 'unknown',
            'gameName': gameNameInput ? gameNameInput.value : 'unknown',
            'modified': formModified.toString(),
            'action': buttonValue
        });
    }
}

/**
 * Initialize event listeners
 */
document.addEventListener('DOMContentLoaded', function() {
    const form = document.querySelector('#editGameForm');
    if (!form) return;

    // Track form changes
    form.addEventListener('change', trackFormChange);

    // Track form submission (before validation)
    form.addEventListener('submit', trackBeforeSubmit);

    // Track when user starts editing
    document.querySelectorAll('.form-control').forEach(input => {
        input.addEventListener('focus', function() {
            trackFormStart(this.name);
        });
    });
});
