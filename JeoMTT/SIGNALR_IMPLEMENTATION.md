# Real-Time Session Updates with SignalR Implementation

## Overview

This document describes the SignalR implementation for real-time player joining, answer submission, and score updates in JeoMTT.

## Architecture

### Components

1. **GameSessionHub** (`Hubs/GameSessionHub.cs`)
   - Server-side SignalR hub
   - Handles all real-time communication
   - Manages groups per session
   - Processes answer verification with 2-second delay

2. **GameSessionClient** (`wwwroot/js/game-session-client.js`)
   - Client-side SignalR wrapper
   - Manages connection lifecycle
   - Handles automatic reconnection
   - Provides method to submit answers and join sessions

3. **GameBoard.cshtml**
   - Updated to use SignalR for real-time updates
   - Real-time leaderboard updates
   - Instant answer verification feedback
   - Automatic score and time updates

## Features

### 1. Player Joining
- When a player joins via SessionController, SignalR notifies all connected players in that session
- Event: `PlayerJoined` - broadcasts to entire session group
- Updates: Player count, leaderboard display

### 2. Answer Submission Flow

```
Player Submits Answer
         ↓
Client sends via SignalR.invoke("SubmitAnswer", ...)
         ↓
Server validates (answer not empty, question exists, not already answered)
         ↓
Server broadcasts "AnswerReceived" → Client shows "Verifying..."
         ↓
Server waits 2 seconds (ANSWER_VERIFICATION_DELAY_MS = 2000)
         ↓
Server compares answer with correct answer (case-insensitive)
         ↓
Server updates score if correct
         ↓
Server sends "AnswerVerified" → Client shows result
         ↓
Server broadcasts "SessionUpdated" → All clients update leaderboard & scores
```

### 3. Real-Time Updates

**PlayerJoined Event**
```javascript
{
    playerNickname: "John",
    joinedAt: "2026-01-29T10:30:00Z",
    message: "John has joined the session!"
}
```

**AnswerReceived Event**
```javascript
{
    playerResponse: "Paris",
    questionId: "guid",
    message: "Verifying answer..."
}
```

**AnswerVerified Event**
```javascript
{
    isCorrect: true,
    correctAnswer: "Paris",
    pointsEarned: 200,
    newScore: 500,
    message: "Correct! You earned 200 points!"
}
```

**SessionUpdated Event**
```javascript
{
    sessionId: "guid",
    playerCount: 3,
    currentPlayerScore: 500,
    topPlayerScore: 750,
    leaderboard: [
        { rank: 1, playerNickname: "Alice", score: 750, joinedAt: "..." },
        { rank: 2, playerNickname: "John", score: 500, joinedAt: "..." },
        { rank: 3, playerNickname: "Bob", score: 250, joinedAt: "..." }
    ],
    timeRemaining: 3600,
    answeredQuestionsCount: 5,
    status: "Active"
}
```

## Client-Side Events

The following event handlers can be set in JavaScript:

```javascript
window.onPlayerJoined = function(data) { }          // Player joined
window.onAnswerReceived = function(data) { }        // Answer being verified
window.onAnswerVerified = function(data) { }        // Answer result
window.onSessionUpdated = function(data) { }        // Session state changed
window.onAnswerValidationError = function(msg) { }  // Answer validation failed
window.onServerError = function(msg) { }            // Server error
```

## Connection Management

### Automatic Reconnection
- Configured with exponential backoff: [0, 0, 1000, 3000, 5000, 10000] ms
- Automatically rejoins session on reconnection
- Works seamlessly across network interruptions

### Session Groups
- Each session uses a group: `session-{sessionId}`
- When player connects, automatically added to group
- When player disconnects, automatically removed from group

## Azure Deployment Considerations

### Sticky Sessions
For Azure App Service with multiple instances, use:
```csharp
builder.Services.AddSignalR()
    .AddAzureSignalR(options => 
    {
        options.ClaimTypeList.Add("sub"); // Enable sticky sessions
    });
```

### Configuration in appsettings.json (Azure)
```json
{
  "Azure:SignalR:ConnectionString": "Endpoint=https://xxx.service.signalr.net;AccessKey=xxx;Version=1.0;"
}
```

### Local Development
Works out of the box with default in-process SignalR.

## Answer Verification Delay

The 2-second verification delay provides:
1. **UX Feedback**: Shows "Verifying answer..." message
2. **Tension Building**: Creates suspense similar to real Jeopardy
3. **Server Load Distribution**: Spreads answer processing
4. **Consistency**: All players see the same delay

Can be adjusted in `GameSessionHub.cs`:
```csharp
private const int ANSWER_VERIFICATION_DELAY_MS = 2000;
```

## Browser Compatibility

Works with:
- Modern browsers (Chrome, Firefox, Safari, Edge)
- Requires WebSocket support (fallback to Server-Sent Events available)
- Mobile browsers supported

## Performance

- SignalR uses efficient binary protocol by default
- Auto-negotiates best transport (WebSocket → SSE → LongPolling)
- Scales to thousands of concurrent connections
- Azure SignalR Service can be used for even higher scale

## Troubleshooting

### Connection Issues
1. Check firewall/proxy allows WebSocket connections
2. Verify hub route is registered: `/gamehub`
3. Check browser console for JavaScript errors

### Answer Not Submitting
1. Verify SignalR connection is active
2. Check browser Network tab for `/gamehub/negotiate`
3. Ensure question ID is valid

### Scores Not Updating
1. Verify `SessionUpdated` event is firing
2. Check browser console for errors
3. Verify database has the answer record

## Testing

### Local Testing
1. Open two browser tabs to same GameBoard URL
2. Join in one tab, verify notification in other
3. Submit answer, verify 2-second delay then result
4. Check that leaderboard updates in real-time

### Network Simulation
Use Chrome DevTools Network tab to throttle:
1. Good 3G - Verify reconnection
2. Offline - Disconnect, verify reconnection message
3. Custom latency - Add 2000ms delay to test robustness

## Migration Notes

- Replaces polling-based updates
- Previous `/Session/GetSessionData` endpoint still available for backward compatibility
- No database changes required
- Existing session and player data structures unchanged
