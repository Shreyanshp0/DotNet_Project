document.addEventListener('DOMContentLoaded', () => {
    const moodKey = 'mindcare-mood-entries';
    const readMoods = () => JSON.parse(localStorage.getItem(moodKey) || '[]');
    const writeMoods = entries => localStorage.setItem(moodKey, JSON.stringify(entries));
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

    const renderMoods = () => {
        const entries = readMoods().filter(entry => entry.date === today);
        const list = document.getElementById('moodEntries');
        const empty = document.getElementById('moodEmpty');
        const count = document.getElementById('moodCount');
        if (!list) return;
        if (count) count.textContent = `${entries.length} ${entries.length === 1 ? 'entry' : 'entries'}`;
        if (!entries.length) {
            list.innerHTML = '';
            empty?.classList.remove('d-none');
            return;
        }
        empty?.classList.add('d-none');
        list.innerHTML = entries.slice().reverse().map(entry => `<div class="entry-row"><span class="entry-emoji">${entry.emoji}</span><div><strong>${entry.mood}</strong><small>${entry.reason || 'No explanation shared'}</small></div><time>${entry.time}</time></div>`).join('');
    };

    const saveMood = () => {
        if (!selectedMood) return;
        const reason = document.getElementById('moodReason')?.value.trim() || '';
        const now = new Date();
        const entries = readMoods();
        entries.push({ ...selectedMood, reason, date: today, time: now.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }) });
        writeMoods(entries);
        document.getElementById('moodFollowUp')?.classList.add('d-none');
        document.getElementById('moodSuccess')?.classList.remove('d-none');
        document.getElementById('moodReason')?.setAttribute('value', '');
        renderMoods();
    };
    document.getElementById('saveMoodButton')?.addEventListener('click', saveMood);
    document.getElementById('skipMoodButton')?.addEventListener('click', saveMood);
    renderMoods();

    document.querySelectorAll('[data-prefer]').forEach(checkbox => checkbox.addEventListener('change', () => {
        const input = document.getElementById(checkbox.dataset.prefer);
        if (!input) return;
        input.value = checkbox.checked ? 'Prefer not to answer' : '';
        input.disabled = checkbox.checked;
    }));
    document.getElementById('journalForm')?.addEventListener('submit', event => {
        event.preventDefault();
        document.getElementById('journalSubmit')?.setAttribute('disabled', 'disabled');
        document.getElementById('journalSuccess')?.classList.remove('d-none');
        localStorage.setItem('mindcare-journal-today', 'submitted');
    });

    const form = document.getElementById('childAssessmentForm');
    if (form) {
        const cards = [...form.querySelectorAll('.question-card')];
        const previous = document.getElementById('childPrevious');
        const next = document.getElementById('childNext');
        const reviewButton = document.getElementById('childReview');
        const submit = document.getElementById('childSubmit');
        const review = document.getElementById('assessmentReview');
        const questions = document.getElementById('assessmentQuestions');
        const progressBar = document.getElementById('assessmentProgressBar');
        const progressText = document.getElementById('assessmentProgressText');
        const stepLabel = document.getElementById('assessmentStepLabel');
        let current = 0;
        let reviewing = false;
        const showError = card => {
            card.classList.add('question-error');
            let message = card.querySelector('.question-validation');
            if (!message) { message = document.createElement('p'); message.className = 'question-validation'; message.setAttribute('role', 'alert'); card.append(message); }
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
        form.addEventListener('submit', event => { event.preventDefault(); if (reviewing && window.confirm('Submit this assessment for your parent to review?')) window.location.href = '/Assessment/Submitted'; });
        update();
    }

    const activitySamples = {
        '2026-10-14': { moods: [['🙂', 'Good', 'Feeling okay about school', '9:12 AM']], journal: 'No journal submitted yet.', journalDate: 'Pending for today' },
        '2026-10-13': { moods: [['😄', 'Great', 'Had fun at art club', '8:25 AM'], ['🙂', 'Good', 'No explanation shared', '4:40 PM']], journal: '“Finished my art project and had lunch with my friends.”', journalDate: 'Submitted October 13 at 7:48 PM' },
        '2026-10-07': { moods: [['😐', 'Okay', 'No explanation shared', '1:40 PM']], journal: 'No journal submitted for this date.', journalDate: 'No entry available' }
    };
    document.querySelectorAll('.date-choice').forEach(button => button.addEventListener('click', () => {
        document.querySelectorAll('.date-choice').forEach(item => item.classList.remove('active'));
        button.classList.add('active');
        const date = new Date(`${button.dataset.date}T12:00:00`);
        const label = document.getElementById('activityDateLabel');
        if (label) label.textContent = `Activity for ${date.toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' })}`;
        const sample = activitySamples[button.dataset.date];
        if (sample) {
            const moods = document.getElementById('parentMoodEntries');
            if (moods) moods.innerHTML = sample.moods.map(entry => `<div class="entry-row"><span class="entry-emoji">${entry[0]}</span><div><strong>${entry[1]}</strong><small>${entry[2]}</small></div><time>${entry[3]}</time></div>`).join('');
            const journal = document.getElementById('parentJournalQuote');
            const journalDate = document.getElementById('parentJournalDate');
            if (journal) journal.textContent = sample.journal;
            if (journalDate) journalDate.textContent = sample.journalDate;
        }
    }));
});
