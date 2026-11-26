document.addEventListener('DOMContentLoaded', () => {
    const getCsrfToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const recordCompletion = async (exerciseType, durationSeconds) => {
        try {
            const token = getCsrfToken();
            await fetch('/api/relaxation/complete', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'X-CSRF-TOKEN': token
                },
                body: JSON.stringify({
                    exerciseType,
                    durationSeconds: Math.max(0, Math.min(3600, Math.round(durationSeconds))),
                    wasCompleted: true
                })
            });
        } catch {
            // Optional tracking: never disrupt the child's activity if offline or unavailable.
        }
    };

    // 1. BREATHING EXERCISE
    const breathingRoot = document.getElementById('breathingApp');
    if (breathingRoot) {
        const circle = document.getElementById('breathingCircle');
        const stageLabel = document.getElementById('breathingStageLabel');
        const countdownEl = document.getElementById('breathingCountdown');
        const progressBar = document.getElementById('breathingProgressBar');
        const sessionTimeEl = document.getElementById('breathingSessionTime');
        const modeSelect = document.getElementById('breathingModeSelect');
        const durationSelect = document.getElementById('breathingDurationSelect');
        const startBtn = document.getElementById('breathingStartBtn');
        const pauseBtn = document.getElementById('breathingPauseBtn');
        const resetBtn = document.getElementById('breathingResetBtn');
        const exitLink = document.getElementById('breathingExitBtn');
        const completeBanner = document.getElementById('breathingCompleteBanner');

        const modes = {
            box: [
                { label: 'Inhale', seconds: 4, css: 'phase-inhale' },
                { label: 'Hold', seconds: 4, css: 'phase-hold' },
                { label: 'Exhale', seconds: 4, css: 'phase-exhale' },
                { label: 'Hold', seconds: 4, css: 'phase-rest' }
            ],
            gentle: [
                { label: 'Inhale', seconds: 4, css: 'phase-inhale' },
                { label: 'Exhale', seconds: 4, css: 'phase-exhale' }
            ]
        };

        let timerId = null;
        let isRunning = false;
        let isPaused = false;
        let totalSeconds = parseInt(durationSelect?.value || '60', 10);
        let elapsedSeconds = 0;
        let stageIndex = 0;
        let stageRemaining = 4;

        const clearTimer = () => {
            if (timerId !== null) {
                clearInterval(timerId);
                timerId = null;
            }
        };

        const getActiveStages = () => modes[modeSelect?.value === 'gentle' ? 'gentle' : 'box'];

        const updateUI = () => {
            const stages = getActiveStages();
            const currentStage = stages[stageIndex % stages.length];

            if (!isRunning && elapsedSeconds === 0) {
                circle.className = 'breathing-circle';
                stageLabel.textContent = 'Ready';
                countdownEl.textContent = `${stages[0].seconds}`;
            } else if (elapsedSeconds >= totalSeconds) {
                circle.className = 'breathing-circle';
                stageLabel.textContent = 'Done';
                countdownEl.textContent = '0';
            } else {
                circle.className = `breathing-circle ${currentStage.css}`;
                stageLabel.textContent = currentStage.label;
                countdownEl.textContent = `${stageRemaining}`;
            }

            const pct = totalSeconds > 0 ? Math.min(100, Math.round((elapsedSeconds / totalSeconds) * 100)) : 0;
            if (progressBar) {
                progressBar.style.width = `${pct}%`;
                progressBar.setAttribute('aria-valuenow', String(pct));
            }
            if (sessionTimeEl) {
                const remaining = Math.max(0, totalSeconds - elapsedSeconds);
                const mins = Math.floor(remaining / 60);
                const secs = remaining % 60;
                sessionTimeEl.textContent = `${mins}:${secs.toString().padStart(2, '0')} remaining`;
            }
        };

        const resetSession = () => {
            clearTimer();
            isRunning = false;
            isPaused = false;
            totalSeconds = parseInt(durationSelect?.value || '60', 10);
            elapsedSeconds = 0;
            stageIndex = 0;
            stageRemaining = getActiveStages()[0].seconds;
            completeBanner?.classList.add('d-none');
            if (startBtn) startBtn.disabled = false;
            if (pauseBtn) {
                pauseBtn.disabled = true;
                pauseBtn.textContent = 'Pause';
            }
            if (modeSelect) modeSelect.disabled = false;
            if (durationSelect) durationSelect.disabled = false;
            updateUI();
        };

        const tick = () => {
            if (!isRunning || isPaused) return;

            elapsedSeconds += 1;
            stageRemaining -= 1;

            if (elapsedSeconds >= totalSeconds) {
                clearTimer();
                isRunning = false;
                isPaused = false;
                if (startBtn) startBtn.disabled = false;
                if (pauseBtn) pauseBtn.disabled = true;
                if (modeSelect) modeSelect.disabled = false;
                if (durationSelect) durationSelect.disabled = false;
                completeBanner?.classList.remove('d-none');
                updateUI();
                recordCompletion('Breathing', totalSeconds);
                return;
            }

            if (stageRemaining <= 0) {
                const stages = getActiveStages();
                stageIndex = (stageIndex + 1) % stages.length;
                stageRemaining = stages[stageIndex].seconds;
            }

            updateUI();
        };

        const startSession = () => {
            if (isRunning) return; // Prevent multiple simultaneous timers
            clearTimer();
            totalSeconds = parseInt(durationSelect?.value || '60', 10);
            elapsedSeconds = 0;
            stageIndex = 0;
            stageRemaining = getActiveStages()[0].seconds;
            isRunning = true;
            isPaused = false;
            completeBanner?.classList.add('d-none');

            if (startBtn) startBtn.disabled = true;
            if (pauseBtn) {
                pauseBtn.disabled = false;
                pauseBtn.textContent = 'Pause';
            }
            if (modeSelect) modeSelect.disabled = true;
            if (durationSelect) durationSelect.disabled = true;

            updateUI();
            timerId = setInterval(tick, 1000);
        };

        startBtn?.addEventListener('click', startSession);
        pauseBtn?.addEventListener('click', () => {
            if (!isRunning) return;
            isPaused = !isPaused;
            pauseBtn.textContent = isPaused ? 'Resume' : 'Pause';
            stageLabel.textContent = isPaused ? 'Paused' : getActiveStages()[stageIndex % getActiveStages().length].label;
        });
        resetBtn?.addEventListener('click', resetSession);
        modeSelect?.addEventListener('change', resetSession);
        durationSelect?.addEventListener('change', resetSession);
        exitLink?.addEventListener('click', clearTimer);
        window.addEventListener('pagehide', clearTimer);
        window.addEventListener('beforeunload', clearTimer);

        resetSession();
    }

    // 2. 5-4-3-2-1 GROUNDING EXERCISE
    const groundingRoot = document.getElementById('groundingApp');
    if (groundingRoot) {
        const steps = [
            { num: 5, sense: 'See', icon: 'bi-eye', instruction: 'Look around gently and notice five things you can see.' },
            { num: 4, sense: 'Feel', icon: 'bi-hand-index-thumb', instruction: 'Notice four things you can feel, like your feet on the floor or your clothes.' },
            { num: 3, sense: 'Hear', icon: 'bi-ear', instruction: 'Listen softly and notice three things you can hear nearby or far away.' },
            { num: 2, sense: 'Smell', icon: 'bi-wind', instruction: 'Notice two things you can smell, or think of two scents you like.' },
            { num: 1, sense: 'Taste', icon: 'bi-cup-straw', instruction: 'Notice one thing you can taste, or take a slow sip of water.' }
        ];

        const numberEl = document.getElementById('groundingNumber');
        const senseEl = document.getElementById('groundingSense');
        const instructionEl = document.getElementById('groundingInstruction');
        const progressEl = document.getElementById('groundingProgress');
        const progressBar = document.getElementById('groundingProgressBar');
        const notesEl = document.getElementById('groundingOptionalNotes');
        const prevBtn = document.getElementById('groundingPrevBtn');
        const nextBtn = document.getElementById('groundingNextBtn');
        const skipBtn = document.getElementById('groundingSkipBtn');
        const stepContainer = document.getElementById('groundingStepContainer');
        const completeCard = document.getElementById('groundingCompleteCard');
        const restartBtn = document.getElementById('groundingRestartBtn');

        let currentIndex = 0;
        const startedAt = Date.now();
        let completionRecorded = false;

        const renderStep = () => {
            const step = steps[currentIndex];
            if (numberEl) numberEl.textContent = String(step.num);
            if (senseEl) senseEl.innerHTML = `<i class="bi ${step.icon} me-2"></i>Notice ${step.num} things you can ${step.sense.toLowerCase()}`;
            if (instructionEl) instructionEl.textContent = step.instruction;
            if (progressEl) progressEl.textContent = `Step ${currentIndex + 1} of ${steps.length}`;
            if (progressBar) {
                const pct = Math.round(((currentIndex + 1) / steps.length) * 100);
                progressBar.style.width = `${pct}%`;
            }
            if (notesEl) notesEl.value = '';
            if (prevBtn) prevBtn.disabled = currentIndex === 0;
            if (nextBtn) nextBtn.textContent = currentIndex === steps.length - 1 ? 'Finish' : 'Next';
        };

        const advance = () => {
            if (currentIndex < steps.length - 1) {
                currentIndex += 1;
                renderStep();
            } else {
                stepContainer?.classList.add('d-none');
                completeCard?.classList.remove('d-none');
                if (!completionRecorded) {
                    completionRecorded = true;
                    const duration = Math.max(5, Math.round((Date.now() - startedAt) / 1000));
                    recordCompletion('Grounding', duration);
                }
            }
        };

        prevBtn?.addEventListener('click', () => {
            if (currentIndex > 0) {
                currentIndex -= 1;
                renderStep();
            }
        });
        nextBtn?.addEventListener('click', advance);
        skipBtn?.addEventListener('click', advance);
        restartBtn?.addEventListener('click', () => {
            currentIndex = 0;
            completionRecorded = false;
            completeCard?.classList.add('d-none');
            stepContainer?.classList.remove('d-none');
            renderStep();
        });

        renderStep();
    }

    // 3. POSITIVE AFFIRMATIONS
    const affirmationsRoot = document.getElementById('affirmationsApp');
    if (affirmationsRoot) {
        const affirmations = [
            'I am learning something new every day.',
            'It is okay to make mistakes.',
            'My feelings matter.',
            'I can ask someone for help.',
            "I don't have to be perfect.",
            'I am proud of trying.',
            'I can take things one step at a time.',
            'I deserve kindness and respect.'
        ];

        const favStorageKey = 'mindcare-favorite-affirmations';
        const card = document.getElementById('affirmationCard');
        const quoteEl = document.getElementById('affirmationQuote');
        const counterEl = document.getElementById('affirmationCounter');
        const prevBtn = document.getElementById('affirmationPrevBtn');
        const nextBtn = document.getElementById('affirmationNextBtn');
        const skipBtn = document.getElementById('affirmationSkipBtn');
        const shuffleBtn = document.getElementById('affirmationShuffleBtn');
        const favBtn = document.getElementById('affirmationFavBtn');
        const favCountEl = document.getElementById('affirmationFavCount');
        const completeBtn = document.getElementById('affirmationDoneBtn');
        const doneBanner = document.getElementById('affirmationDoneBanner');

        let order = affirmations.map((_, i) => i);
        let index = 0;
        let favorites = [];
        try {
            const raw = localStorage.getItem(favStorageKey);
            const parsed = raw ? JSON.parse(raw) : [];
            if (Array.isArray(parsed)) {
                favorites = parsed.filter(n => Number.isInteger(n) && n >= 0 && n < affirmations.length);
            }
        } catch {
            favorites = [];
        }

        const saveFavorites = () => {
            try {
                localStorage.setItem(favStorageKey, JSON.stringify(favorites));
            } catch {
                // Ignore storage quota errors
            }
        };

        const renderAffirmation = () => {
            const actualIndex = order[index];
            const isFav = favorites.includes(actualIndex);

            card?.classList.add('is-transitioning');
            setTimeout(() => {
                if (quoteEl) quoteEl.textContent = affirmations[actualIndex];
                if (counterEl) counterEl.textContent = `${index + 1} of ${order.length}`;
                if (favBtn) {
                    favBtn.innerHTML = isFav
                        ? '<i class="bi bi-heart-fill text-danger me-1"></i> Favourited'
                        : '<i class="bi bi-heart me-1"></i> Favourite';
                    favBtn.setAttribute('aria-pressed', isFav ? 'true' : 'false');
                }
                if (favCountEl) {
                    favCountEl.textContent = `${favorites.length} saved`;
                }
                card?.classList.remove('is-transitioning');
            }, 120);
        };

        prevBtn?.addEventListener('click', () => {
            index = (index - 1 + order.length) % order.length;
            renderAffirmation();
        });

        const goNext = () => {
            index = (index + 1) % order.length;
            renderAffirmation();
        };

        nextBtn?.addEventListener('click', goNext);
        skipBtn?.addEventListener('click', goNext);

        shuffleBtn?.addEventListener('click', () => {
            for (let i = order.length - 1; i > 0; i--) {
                const j = Math.floor(Math.random() * (i + 1));
                [order[i], order[j]] = [order[j], order[i]];
            }
            index = 0;
            renderAffirmation();
        });

        favBtn?.addEventListener('click', () => {
            const actualIndex = order[index];
            if (favorites.includes(actualIndex)) {
                favorites = favorites.filter(i => i !== actualIndex);
            } else {
                favorites.push(actualIndex);
            }
            saveFavorites();
            renderAffirmation();
        });

        completeBtn?.addEventListener('click', () => {
            doneBanner?.classList.remove('d-none');
            recordCompletion('Affirmations', 30);
        });

        renderAffirmation();
    }
});
