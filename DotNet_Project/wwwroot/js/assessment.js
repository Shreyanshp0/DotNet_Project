document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('assessmentForm');
    if (!form) return;

    const questions = [...form.querySelectorAll('.question-card')];
    const previous = document.getElementById('previousButton');
    const next = document.getElementById('nextButton');
    const submit = document.getElementById('submitButton');
    const progressBar = document.getElementById('progressBar');
    const progressText = document.getElementById('progressText');
    let current = 0;

    const update = () => {
        questions.forEach((question, index) => question.classList.toggle('d-none', index !== current));
        const number = current + 1;
        progressBar.style.width = `${(number / questions.length) * 100}%`;
        progressText.textContent = `${number} of ${questions.length}`;
        previous.disabled = current === 0;
        next.classList.toggle('d-none', current === questions.length - 1);
        submit.classList.toggle('d-none', current !== questions.length - 1);
    };

    const answered = () => {
        const card = questions[current];
        const selected = card.querySelector('input[type="radio"]:checked');
        card.querySelector('.question-validation')?.remove();
        if (!selected) {
            card.classList.add('question-error');
            const message = document.createElement('p');
            message.className = 'question-validation';
            message.setAttribute('role', 'alert');
            message.innerHTML = '<i class="bi bi-exclamation-circle me-1"></i> Please choose an answer before continuing.';
            card.append(message);
            setTimeout(() => card.classList.remove('question-error'), 550);
            return false;
        }
        return true;
    };

    next.addEventListener('click', () => { if (answered()) { current += 1; update(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });
    previous.addEventListener('click', () => { if (current > 0) { current -= 1; update(); window.scrollTo({ top: 0, behavior: 'smooth' }); } });
    form.addEventListener('submit', event => {
        event.preventDefault();
        if (answered() && window.confirm('Are you ready to submit your assessment? You can review your answer on this screen before continuing.')) {
            window.location.href = '/Assessment/Submitted';
        }
    });
    update();
});
