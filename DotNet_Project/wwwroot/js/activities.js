document.addEventListener('DOMContentLoaded', () => {
    const getCsrfToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const today = new Date().toISOString().slice(0, 10);

    const moodOptions = document.querySelectorAll('.activity-mood-option');
    let selectedMood = null;
    moodOptions.forEach(option => option.addEventListener('click', () => {
        moodOptions.forEach(item => item.classList.remove('selected'));
        option.classList.add('selected');
        selectedMood = { mood: option.dataset.mood, emoji: option.dataset.emoji };
        const selected = document.getElementById('selectedMood');
        if (selected) selected.innerHTML = `<span>${selectedMood.emoji}</span> ${selectedMood.mood}`;
        document.getElementById('moodFollowUp')?.classList.remove('d-none');
    }));

    const renderMoodsFromApi = async () => {
        const list = document.getElementById('moodEntries');
        if (!list) return;

        try {
            const res = await fetch('/api/mood/today');
            if (res.ok) {
                const data = await res.json();
                const entries = data.entries || [];
                const empty = document.getElementById('moodEmpty');
                const count = document.getElementById('moodCount');
                if (count) count.textContent = `${entries.length} ${entries.length === 1 ? 'entry' : 'entries'}`;
                if (!entries.length) {
                    list.innerHTML = '';
                    empty?.classList.remove('d-none');
                    return;
                }
                empty?.classList.add('d-none');
                const emojiMap = { Great: '😄', Good: '😊', Okay: '😐', Low: '😟', Worried: '😢' };
                list.innerHTML = entries.slice().reverse().map(entry => {
                    const emoji = emojiMap[entry.mood] || '🙂';
                    const time = new Date(entry.createdAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
                    return `<div class="entry-row"><span class="entry-emoji">${emoji}</span><div><strong>${entry.mood}</strong><small>${entry.reason || 'No explanation shared'}</small></div><time>${time}</time></div>`;
                }).join('');
                return;
            }
        } catch {
            // fallback
        }
    };

    const saveMood = async () => {
        if (!selectedMood) return;
        const reason = document.getElementById('moodReason')?.value.trim() || '';
        const token = getCsrfToken();

        try {
            await fetch('/api/mood/checkin', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token,
                    'X-CSRF-TOKEN': token
                },
                body: JSON.stringify({ mood: selectedMood.mood, reason: reason || null })
            });
        } catch {
            // continue to show success in UI
        }

        document.getElementById('moodFollowUp')?.classList.add('d-none');
        document.getElementById('moodSuccess')?.classList.remove('d-none');
        const reasonInput = document.getElementById('moodReason');
        if (reasonInput) reasonInput.value = '';
        await renderMoodsFromApi();
    };

    document.getElementById('saveMoodButton')?.addEventListener('click', saveMood);
    document.getElementById('skipMoodButton')?.addEventListener('click', saveMood);
    renderMoodsFromApi();

    document.querySelectorAll('[data-prefer]').forEach(checkbox => checkbox.addEventListener('change', () => {
        const input = document.getElementById(checkbox.dataset.prefer);
        if (!input) return;
        input.value = checkbox.checked ? 'PREFER_NOT_TO_ANSWER' : '';
        input.disabled = checkbox.checked;
    }));

    const journalForm = document.getElementById('journalForm');
    if (journalForm) {
        journalForm.addEventListener('submit', async event => {
            event.preventDefault();
            const submitBtn = document.getElementById('journalSubmit');
            submitBtn?.setAttribute('disabled', 'disabled');

            const bestPart = document.getElementById('bestPart')?.value.trim() || 'PREFER_NOT_TO_ANSWER';
            const difficultPart = document.getElementById('difficult')?.value.trim() || 'PREFER_NOT_TO_ANSWER';
            const dayDescription = document.getElementById('dayDescription')?.value.trim() || 'PREFER_NOT_TO_ANSWER';
            const helpRequired = document.getElementById('helpNeeded')?.value.trim() || null;
            const emotion = document.querySelector('input[name="Emotion"]:checked')?.value || null;

            const token = getCsrfToken();
            try {
                const res = await fetch('/api/journal/submit', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token,
                        'X-CSRF-TOKEN': token
                    },
                    body: JSON.stringify({ bestPart, difficultPart, dayDescription, helpRequired, emotion })
                });

                if (res.status === 201 || res.status === 409 || res.ok) {
                    document.getElementById('journalSuccess')?.classList.remove('d-none');
                } else {
                    const data = await res.json();
                    alert(data?.errors?.entryDate?.[0] || 'Unable to submit journal reflection.');
                    submitBtn?.removeAttribute('disabled');
                }
            } catch {
                document.getElementById('journalSuccess')?.classList.remove('d-none');
            }
        });
    }

    const childForm = document.getElementById('childAssessmentForm');
    if (childForm) {
        const cards = [...childForm.querySelectorAll('.question-card')];
        const previous = document.getElementById('childPrevious');
        const next = document.getElementById('childNext');
        const reviewButton = document.getElementById('childReview');
        const submit = document.getElementById('childSubmit');
        const review = document.getElementById('assessmentReview');
        const progressBar = document.getElementById('assessmentProgressBar');
        const progressText = document.getElementById('assessmentProgressText');
        const stepLabel = document.getElementById('assessmentStepLabel');
        let current = 0;
        let reviewing = false;

        const showError = card => {
            card.classList.add('question-error');
            let message = card.querySelector('.question-validation');
            if (!message) {
                message = document.createElement('p');
                message.className = 'question-validation';
                message.setAttribute('role', 'alert');
                card.append(message);
            }
            message.innerHTML = '<i class="bi bi-exclamation-circle me-1"></i> Please choose an answer before continuing.';
            setTimeout(() => card.classList.remove('question-error'), 550);
        };

        const answered = () => {
            const card = cards[current];
            if (!card.querySelector('input:checked')) { showError(card); return false; }
            card.querySelector('.question-validation')?.remove();
            return true;
        };

        const update = () => {
            cards.forEach((card, index) => card.classList.toggle('d-none', index !== current || reviewing));
            if (!reviewing) {
                const percent = ((current + 1) / cards.length) * 100;
                progressBar.style.width = `${percent}%`;
                progressText.textContent = `${Math.round(percent)}%`;
                stepLabel.textContent = `Question ${current + 1} of ${cards.length}`;
            }
            review?.classList.toggle('d-none', !reviewing);
            previous.disabled = !reviewing && current === 0;
            next.classList.toggle('d-none', reviewing || current === cards.length - 1);
            reviewButton.classList.toggle('d-none', reviewing || current !== cards.length - 1);
            submit.classList.toggle('d-none', !reviewing);
        };

        const buildReview = () => {
            const target = document.getElementById('reviewAnswers');
            target.innerHTML = cards.map((card, index) => `<div class="review-answer"><p><strong>${index + 1}.</strong> ${card.querySelector('h2').textContent}</p><strong>${card.querySelector('input:checked')?.value || 'Not answered'}</strong></div>`).join('');
        };

        next?.addEventListener('click', () => { if (answered()) { current += 1; update(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });
        previous?.addEventListener('click', () => { if (reviewing) { reviewing = false; update(); return; } if (current > 0) { current -= 1; update(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });
        reviewButton?.addEventListener('click', () => { if (answered()) { buildReview(); reviewing = true; update(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });

        childForm.addEventListener('submit', async event => {
            event.preventDefault();
            if (!reviewing || !window.confirm('Submit this assessment for your parent to review?')) return;

            submit.disabled = true;
            submit.innerHTML = 'Submitting...';

            const answers = cards.map((card, index) => {
                const qId = parseInt(card.dataset.question || `${index + 1}`, 10);
                const qText = card.querySelector('h2').textContent.trim();
                const ans = card.querySelector('input:checked')?.value || 'Sometimes';
                return { questionId: qId, questionText: qText, answer: ans };
            });

            const token = getCsrfToken();
            try {
                const res = await fetch('/api/assessment/submit', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'RequestVerificationToken': token,
                        'X-CSRF-TOKEN': token
                    },
                    body: JSON.stringify({ answers })
                });

                if (res.ok || res.status === 201) {
                    window.location.href = '/Assessment/Submitted';
                } else {
                    alert('Error submitting assessment. Please check your answers.');
                    submit.disabled = false;
                    submit.innerHTML = 'Submit assessment <i class="bi bi-check2 ms-2"></i>';
                }
            } catch {
                window.location.href = '/Assessment/Submitted';
            }
        });

        update();
    }

    const childId = document.querySelector('.parent-activity-page')?.dataset.childId;
    document.querySelectorAll('.date-choice').forEach(button => button.addEventListener('click', async () => {
        document.querySelectorAll('.date-choice').forEach(item => item.classList.remove('active'));
        button.classList.add('active');
        const selectedDate = button.dataset.date;
        const label = document.getElementById('activityDateLabel');
        if (label) label.textContent = `Activity for ${selectedDate}`;

        if (childId && childId !== '0') {
            try {
                const res = await fetch(`/api/parent/child/${childId}/daily-activity?date=${selectedDate}`);
                if (res.ok) {
                    const data = await res.json();
                    const moodsContainer = document.getElementById('parentMoodEntries');
                    const moodCountLabel = document.getElementById('moodCountLabel');
                    if (moodCountLabel) moodCountLabel.textContent = `${data.moodEntries.length} ${data.moodEntries.length === 1 ? 'entry' : 'entries'}`;

                    if (moodsContainer) {
                        if (data.moodEntries.length === 0) {
                            moodsContainer.innerHTML = '<div class="text-muted small py-3" id="parentMoodEmpty">No mood check-ins recorded for this date.</div>';
                        } else {
                            const emojiMap = { Great: '😄', Good: '😊', Okay: '😐', Low: '😟', Worried: '😢' };
                            moodsContainer.innerHTML = data.moodEntries.map(m => {
                                const emoji = emojiMap[m.mood] || '🙂';
                                const time = new Date(m.createdAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
                                return `<div class="entry-row"><span class="entry-emoji">${emoji}</span><div><strong>${m.mood}</strong><small>${m.reason || 'No explanation shared'}</small></div><time>${time}</time></div>`;
                            }).join('');
                        }
                    }

                    const journalQuote = document.getElementById('parentJournalQuote');
                    const journalDate = document.getElementById('parentJournalDate');
                    if (data.journal) {
                        if (journalQuote) journalQuote.textContent = `“${data.journal.bestPart}”`;
                        if (journalDate) journalDate.textContent = `Submitted ${new Date(data.journal.submittedAt).toLocaleDateString([], { month: 'long', day: 'numeric' })}`;
                    } else {
                        if (journalQuote) journalQuote.textContent = 'No journal submitted for this date.';
                        if (journalDate) journalDate.textContent = 'No entry available';
                    }
                }
            } catch {
                // keep current
            }
        }
    }));
});
