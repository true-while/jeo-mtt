import React, { useState, useEffect, useRef } from 'react';

/**
 * TimerComponent - Server-synchronized countdown timer for Jeopardy rounds
 * 
 * Props:
 * - remainingSeconds: Current time remaining from server (updated via SignalR)
 * - isActive: Whether the timer is actively counting down
 * - onTimerExpired: Callback when timer reaches 0
 */
const TimerComponent = ({ remainingSeconds = 30, isActive = false, onTimerExpired = null }) => {
    const [displayTime, setDisplayTime] = useState(remainingSeconds);
    const [timerColor, setTimerColor] = useState('#ffc107'); // Yellow
    const timerIntervalRef = useRef(null);
    const lastUpdateTimeRef = useRef(Date.now());

    useEffect(() => {
        setDisplayTime(remainingSeconds);
        lastUpdateTimeRef.current = Date.now();

        // Update color based on remaining time
        if (remainingSeconds <= 5) {
            setTimerColor('#dc3545'); // Red
        } else if (remainingSeconds <= 10) {
            setTimerColor('#ffc107'); // Yellow
        } else {
            setTimerColor('#28a745'); // Green
        }
    }, [remainingSeconds]);

    useEffect(() => {
        if (!isActive || remainingSeconds <= 0) {
            if (timerIntervalRef.current) {
                clearInterval(timerIntervalRef.current);
                timerIntervalRef.current = null;
            }
            if (remainingSeconds <= 0 && onTimerExpired) {
                onTimerExpired();
            }
            return;
        }

        // Client-side interpolation between server updates
        // This ensures smooth countdown even if server updates are not frequent
        const startTime = Date.now();
        const startSeconds = remainingSeconds;

        timerIntervalRef.current = setInterval(() => {
            const elapsedMs = Date.now() - startTime;
            const elapsedSeconds = Math.floor(elapsedMs / 1000);
            const newDisplayTime = Math.max(0, startSeconds - elapsedSeconds);

            setDisplayTime(newDisplayTime);

            // Update color
            if (newDisplayTime <= 5) {
                setTimerColor('#dc3545'); // Red
            } else if (newDisplayTime <= 10) {
                setTimerColor('#ffc107'); // Yellow
            } else {
                setTimerColor('#28a745'); // Green
            }

            // Trigger callback when timer expires
            if (newDisplayTime <= 0) {
                clearInterval(timerIntervalRef.current);
                timerIntervalRef.current = null;
                if (onTimerExpired) {
                    onTimerExpired();
                }
            }
        }, 100); // Update every 100ms for smooth display

        return () => {
            if (timerIntervalRef.current) {
                clearInterval(timerIntervalRef.current);
                timerIntervalRef.current = null;
            }
        };
    }, [isActive, remainingSeconds, onTimerExpired]);

    return (
        <div
            className="timer-display"
            style={{
                fontSize: '2.5rem',
                fontWeight: 'bold',
                color: timerColor,
                minWidth: '100px',
                textAlign: 'right',
                transition: 'color 0.2s ease',
            }}
        >
            {displayTime}s
        </div>
    );
};

export default TimerComponent;
