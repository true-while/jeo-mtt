# React Timer Component Implementation Guide

## Overview

The timer has been converted to a **React-based TimerComponent** that receives synchronized server-side updates via SignalR. This ensures all players and admins see the exact same countdown.

## File Structure

- **`wwwroot/js/TimerComponent.jsx`** - React component for rendering the timer
- **`wwwroot/js/game-session-client.js`** - SignalR client that listens for `TimerUpdate` events
- **`Hubs/GameSessionHub.cs`** - Server-side timer that broadcasts updates every 100ms

## How It Works

### 1. Server-Side Timer (GameSessionHub.cs)

When a question is selected, the server starts a background timer:

```csharp
// Line 123 in SelectQuestion()
await StartRoundTimer(parsedSessionId, gameRound.Id.ToString(), gameSession.QuestionTimerSeconds);
```

The `StartRoundTimer()` method (line 523) broadcasts timer updates to all clients every 100ms:

```csharp
private async Task StartRoundTimer(Guid sessionId, string roundId, int timerSeconds)
{
    // Calculates expiry time
    var expiryTime = DateTime.UtcNow.AddSeconds(timerSeconds);
    
    // Broadcasts remaining seconds to all clients in session group
    await Clients.Group($"session-{sessionIdStr}").SendAsync("TimerUpdate", new
    {
        roundId = roundId,
        remainingSeconds = remainingSeconds,
        timestamp = DateTime.UtcNow.ToString("O")
    });
    
    // Updates every 100ms for smooth countdown
    await Task.Delay(100);
}
```

### 2. Client-Side Reception (game-session-client.js)

SignalR connection listens for `TimerUpdate` events:

```javascript
// Line 108 in setupEventListeners()
this.connection.on("TimerUpdate", (data) => {
    console.log("TimerUpdate:", data);
    if (window.onTimerUpdate) {
        window.onTimerUpdate(data);
    }
});
```

### 3. React Timer Component (TimerComponent.jsx)

The React component receives timer updates via props and smoothly displays the countdown:

```jsx
const TimerComponent = ({ 
    remainingSeconds = 30,      // Server-provided time
    isActive = false,           // Is timer running?
    onTimerExpired = null       // Callback when timer hits 0
})
```

**Features:**
- ✅ Receives authoritative time from server
- ✅ Smooth 100ms interpolation between server updates
- ✅ Color changes: Green (>10s) → Yellow (5-10s) → Red (≤5s)
- ✅ Automatic cleanup when timer expires
- ✅ Synchronized across all clients

## Integration Points

### In SessionDetails.cshtml (Admin View)

1. Import React and the component:
```html
<script crossorigin src="https://unpkg.com/react@18/umd/react.production.min.js"></script>
<script crossorigin src="https://unpkg.com/react-dom@18/umd/react-dom.production.min.js"></script>
<script src="~/js/TimerComponent.jsx"></script>
```

2. Create a React root for the timer:
```javascript
const timerRoot = ReactDOM.createRoot(document.getElementById('timer-root'));

// Update when timer updates received from SignalR
window.onTimerUpdate = function(data) {
    timerRoot.render(
        <TimerComponent 
            remainingSeconds={data.remainingSeconds} 
            isActive={true}
            onTimerExpired={() => {
                // Auto-show answer when timer expires
                window.gameClient.showAnswer();
            }}
        />
    );
};
```

3. Add a container in the modal:
```html
<div id="timer-root"></div>
```

## Timing Guarantees

| Event | Timing | Sync Method |
|-------|--------|-------------|
| Server broadcasts | Every 100ms | SignalR |
| Client interpolates | Every 100ms | setInterval() |
| Timer expiry | Server-calculated | DateTime.UtcNow |
| Deviation | ±100ms max | Network latency dependent |

## Benefits Over Client-Only Timer

| Aspect | Before | After |
|--------|--------|-------|
| **Sync** | Each client independent | All clients synchronized |
| **Drift** | Can drift by seconds | Max ±100ms drift |
| **Accuracy** | Inconsistent | Server authoritative |
| **Network issues** | No recovery | Server updates every 100ms |
| **Cheating** | Client can manipulate | Server enforces time |

## Props Reference

```jsx
<TimerComponent
  // Remaining seconds (from server)
  remainingSeconds={30}
  
  // Is the timer actively counting down?
  isActive={true}
  
  // Called when timer reaches 0
  onTimerExpired={() => {
    // Handle timer expiration
    window.gameClient.showAnswer();
  }}
/>
```

## Events Flow

```
Admin clicks question
    ↓
SelectQuestion() called
    ↓
Server: StartRoundTimer() begins
    ↓
Server broadcasts "TimerUpdate" every 100ms
    ↓
Client receives: onTimerUpdate(data)
    ↓
React re-renders: <TimerComponent remainingSeconds={n} />
    ↓
Display updates smoothly
    ↓
Server broadcasts "TimerExpired"
    ↓
ShowAnswer() auto-triggered
```

## Babel/JSX Compilation

The `.jsx` file needs to be compiled to JavaScript before use in the browser. Options:

**Option 1: Use Babel CDN (Development)**
```html
<script src="https://unpkg.com/@babel/standalone/babel.min.js"></script>
<script type="text/babel" src="~/js/TimerComponent.jsx"></script>
```

**Option 2: Build pipeline (Production)**
- Use Vite, Webpack, or ASP.NET's bundling
- Compile JSX to JS during build

**Option 3: React without JSX**
If you prefer to avoid compilation, rewrite using `React.createElement()`:
```javascript
// Instead of: <TimerComponent remainingSeconds={30} />
// Use: React.createElement(TimerComponent, { remainingSeconds: 30 })
```

## Next Steps

1. Add React libraries to CDN or bundle
2. Update SessionDetails.cshtml with `<div id="timer-root"></div>`
3. Add React rendering logic in `window.onTimerUpdate` handler
4. Test timer synchronization across multiple clients
5. Monitor browser console for SignalR "TimerUpdate" messages

## Troubleshooting

**Timer doesn't update?**
- Check browser console for SignalR connection status
- Verify `onTimerUpdate` is being called
- Check network tab for TimerUpdate messages

**Timer jumps or stutters?**
- Normal behavior during network latency
- Server re-synchronizes every 100ms
- Should smooth out after client receives update

**Timer doesn't expire?**
- Check `onTimerExpired` callback is set
- Verify `ShowAnswer()` is being called
- Check server logs for timer completion messages
