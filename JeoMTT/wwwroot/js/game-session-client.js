/// <reference path="../lib/signalr/signalr.js" />

/**
 * GameSessionHub Client - Round-based Jeopardy Game
 * Handles real-time communication with the server for game sessions
 * Supports both player and admin modes
 */
class GameSessionClient {
    constructor(sessionId, sessionPlayerId, playerNickname, isAdmin = false) {
        this.sessionId = sessionId;
        this.sessionPlayerId = sessionPlayerId;
        this.playerNickname = playerNickname;
        this.isAdmin = isAdmin;
        this.connection = null;
        this.isConnected = false;
        this.currentRoundId = null;
        this.currentRoundTimer = null;
        this.timerSeconds = 30;
    }

    /**
     * Initialize the SignalR connection
     */    async initialize() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/gamehub")
                .withAutomaticReconnect([0, 0, 1000, 3000, 5000, 10000])
                .build();

            // Set up event listeners
            this.setupEventListeners();

            // Start the connection
            await this.connection.start();
            console.log("SignalR connected");
            this.isConnected = true;

            // Join the session
            await this.joinSession();

            // Join as admin if needed
            if (this.isAdmin) {
                await this.joinAsAdmin();
            }

        } catch (err) {
            console.error("SignalR connection error:", err);
            setTimeout(() => this.initialize(), 5000); // Retry after 5 seconds
        }
    }    /**
     * Setup all SignalR event listeners
     */
    setupEventListeners() {
        // Player joined notification
        this.connection.on("PlayerJoined", (data) => {
            console.log("PlayerJoined:", data);
            if (window.onPlayerJoined) {
                window.onPlayerJoined(data);
            }
        });

        // Question selected - round starts
        this.connection.on("QuestionSelected", (data) => {
            console.log("QuestionSelected:", data);
            this.currentRoundId = data.roundId;
            this.timerSeconds = data.timerSeconds || 30;
            this.startRoundTimer(this.timerSeconds);
            if (window.onQuestionSelected) {
                window.onQuestionSelected(data);
            }
        });

        // Answer submitted by a player (admin receives this)
        this.connection.on("AnswerSubmitted", (data) => {
            console.log("AnswerSubmitted:", data);
            if (window.onAnswerSubmitted) {
                window.onAnswerSubmitted(data);
            }
        });

        // Answer received - thinking state
        this.connection.on("AnswerReceived", (data) => {
            console.log("AnswerReceived:", data);
            if (window.onAnswerReceived) {
                window.onAnswerReceived(data);
            }
        });

        // Answer revealed to all
        this.connection.on("AnswerRevealed", (data) => {
            console.log("AnswerRevealed:", data);
            this.clearRoundTimer();
            if (window.onAnswerRevealed) {
                window.onAnswerRevealed(data);
            }
        });

        // Answer verified result (old flow, kept for compatibility)
        this.connection.on("AnswerVerified", (data) => {
            console.log("AnswerVerified:", data);
            if (window.onAnswerVerified) {
                window.onAnswerVerified(data);
            }
        });

        // Answer marked by admin
        this.connection.on("AnswerMarked", (data) => {
            console.log("AnswerMarked:", data);
            if (window.onAnswerMarked) {
                window.onAnswerMarked(data);
            }
        });

        // Round ended
        this.connection.on("RoundEnded", (data) => {
            console.log("RoundEnded:", data);
            this.clearRoundTimer();
            if (window.onRoundEnded) {
                window.onRoundEnded(data);
            }
        });

        // Round answered
        this.connection.on("RoundAnswered", (data) => {
            console.log("RoundAnswered:", data);
            if (window.onRoundAnswered) {
                window.onRoundAnswered(data);
            }
        });

        // Session updated (scores, leaderboard, etc.)
        this.connection.on("SessionUpdated", (data) => {
            console.log("SessionUpdated:", data);
            if (window.onSessionUpdated) {
                window.onSessionUpdated(data);
            }
        });

        // Answer validation error
        this.connection.on("AnswerValidationError", (message) => {
            console.error("AnswerValidationError:", message);
            if (window.onAnswerValidationError) {
                window.onAnswerValidationError(message);
            }
        });

        // General error
        this.connection.on("Error", (message) => {
            console.error("Server Error:", message);
            if (window.onServerError) {
                window.onServerError(message);
            }
        });

        // Connection reconnected
        this.connection.onreconnected(() => {
            console.log("Reconnected to server");
            this.joinSession();
            if (this.isAdmin) {
                this.joinAsAdmin();
            }
        });

        // Connection closed
        this.connection.onclose(() => {
            console.log("Connection closed");
            this.isConnected = false;
            this.clearRoundTimer();
        });
    }    /**
     * Join the game session
     */
    async joinSession() {
        try {
            await this.connection.invoke("JoinSession", this.sessionId, this.playerNickname);
            console.log("Joined session:", this.sessionId);
        } catch (err) {
            console.error("Error joining session:", err);
        }
    }

    /**
     * Join as admin
     */
    async joinAsAdmin() {
        try {
            await this.connection.invoke("JoinAsAdmin", this.sessionId);
            console.log("Joined session as admin");
        } catch (err) {
            console.error("Error joining as admin:", err);
        }
    }

    /**
     * Admin selects a question to start a round
     */
    async selectQuestion(questionId) {
        try {
            if (!this.isConnected) {
                throw new Error("Not connected to server");
            }
            await this.connection.invoke("SelectQuestion", this.sessionId, questionId);
            console.log("Question selected:", questionId);
        } catch (err) {
            console.error("Error selecting question:", err);
            throw err;
        }
    }

    /**
     * Player submits an answer for the current round
     */
    async submitAnswer(answer) {
        try {
            if (!this.isConnected) {
                throw new Error("Not connected to server");
            }
            if (!this.currentRoundId) {
                throw new Error("No active round");
            }
            await this.connection.invoke("SubmitAnswer", this.sessionId, this.sessionPlayerId, this.currentRoundId, answer);
            console.log("Answer submitted");
        } catch (err) {
            console.error("Error submitting answer:", err);
            throw err;
        }
    }

    /**
     * Admin shows the correct answer
     */
    async showAnswer() {
        try {
            if (!this.isConnected) {
                throw new Error("Not connected to server");
            }
            if (!this.currentRoundId) {
                throw new Error("No active round");
            }
            await this.connection.invoke("ShowAnswer", this.sessionId, this.currentRoundId);
            console.log("Answer revealed");
        } catch (err) {
            console.error("Error showing answer:", err);
            throw err;
        }
    }

    /**
     * Admin ends the current round
     */
    async endRound() {
        try {
            if (!this.isConnected) {
                throw new Error("Not connected to server");
            }
            if (!this.currentRoundId) {
                throw new Error("No active round");
            }
            await this.connection.invoke("EndRound", this.sessionId, this.currentRoundId);
            console.log("Round ended");
        } catch (err) {
            console.error("Error ending round:", err);
            throw err;
        }
    }

    /**
     * Admin marks an answer as correct/incorrect and awards points
     */
    async markAnswer(roundAnswerId, isCorrect, pointsAwarded) {
        try {
            if (!this.isConnected) {
                throw new Error("Not connected to server");
            }
            await this.connection.invoke("MarkAnswer", this.sessionId, roundAnswerId, isCorrect, pointsAwarded);
            console.log("Answer marked");
        } catch (err) {
            console.error("Error marking answer:", err);
            throw err;
        }
    }

    /**
     * Admin marks round as answered and prepares for next round
     */
    async markRoundAsAnswered() {
        try {
            if (!this.isConnected) {
                throw new Error("Not connected to server");
            }
            if (!this.currentRoundId) {
                throw new Error("No active round");
            }
            await this.connection.invoke("MarkRoundAsAnswered", this.sessionId, this.currentRoundId);
            console.log("Round marked as answered");
        } catch (err) {
            console.error("Error marking round as answered:", err);
            throw err;
        }
    }    /**
     * Start a countdown timer for the round
     */
    startRoundTimer(timerSeconds) {
        this.clearRoundTimer();
        let remainingSeconds = timerSeconds;

        const updateTimer = () => {
            // Update timer display
            const timerDisplay = document.getElementById('timerDisplay');
            if (timerDisplay) {
                timerDisplay.textContent = remainingSeconds;
                
                // Add color change as timer gets lower
                if (remainingSeconds <= 5) {
                    timerDisplay.style.color = '#dc3545'; // Red
                } else if (remainingSeconds <= 10) {
                    timerDisplay.style.color = '#ffc107'; // Yellow
                }
            }

            remainingSeconds--;
            if (remainingSeconds < 0) {
                this.clearRoundTimer();
                // Auto-show answer when timer expires
                this.showAnswer();
            } else {
                this.currentRoundTimer = setTimeout(updateTimer, 1000);
            }
        };

        updateTimer();
    }

    /**
     * Clear the current round timer
     */
    clearRoundTimer() {
        if (this.currentRoundTimer) {
            clearTimeout(this.currentRoundTimer);
            this.currentRoundTimer = null;
        }
    }

    /**
     * Disconnect from the session
     */
    async disconnect() {
        try {
            this.clearRoundTimer();
            if (this.connection) {
                await this.connection.stop();
                this.isConnected = false;
                console.log("Disconnected from session");
            }
        } catch (err) {
            console.error("Error disconnecting:", err);
        }
    }
}

// Export for use in pages
window.GameSessionClient = GameSessionClient;
