// Application Insights JavaScript SDK Configuration
// This file initializes Application Insights for client-side telemetry

const appInsightsConfig = {
    instrumentationKey: document.querySelector('meta[name="application-insights-key"]')?.content || '',
    endpointUrl: 'https://dc.applicationinsights.azure.com/v2/track'
};

// Initialize Application Insights SDK
if (appInsightsConfig.instrumentationKey) {
    // Load the Application Insights JavaScript SDK
    const sdkUrl = 'https://az416426.vo.msecnd.net/scripts/b/ai.2.min.js';
    const script = document.createElement('script');
    script.src = sdkUrl;
    script.async = true;
    script.onload = function () {
        window.appInsights = new Microsoft.ApplicationInsights.ApplicationInsights({
            config: {
                instrumentationKey: appInsightsConfig.instrumentationKey,
                enableAutoRouteTracking: true,
                enableAjaxErrorStatusText: true,
                enableResponseHeaderTracking: true,
                namePrefix: "appInsights.",
                samplingPercentage: 100
            }
        });
        window.appInsights.loadAppInsights();
        window.appInsights.trackPageView();
    };
    document.head.appendChild(script);
}

// Helper function to track custom events
function trackEvent(eventName, properties = {}, measurements = {}) {
    if (window.appInsights) {
        window.appInsights.trackEvent({
            name: eventName,
            properties: properties,
            measurements: measurements
        });
    }
}

// Helper function to track exceptions
function trackException(exception, severityLevel = 2) {
    if (window.appInsights) {
        window.appInsights.trackException({
            exception: exception,
            severityLevel: severityLevel
        });
    }
}

// Helper function to track page performance
function trackPerformance(eventName, startTime, properties = {}) {
    if (window.appInsights && startTime) {
        const duration = new Date().getTime() - startTime;
        window.appInsights.trackEvent({
            name: eventName,
            properties: { ...properties, duration: `${duration}ms` }
        });
    }
}
