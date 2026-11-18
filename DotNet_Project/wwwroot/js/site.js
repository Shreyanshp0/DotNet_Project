// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.needs-validation').forEach(form => {
        form.addEventListener('submit', event => {
            const password = form.querySelector('[name="Password"]');
            const confirmation = form.querySelector('[name="ConfirmPassword"]');
            if (password && confirmation) {
                confirmation.setCustomValidity(password.value !== confirmation.value ? 'Passwords do not match.' : '');
            }
            if (!form.checkValidity()) {
                event.preventDefault();
                event.stopPropagation();
                form.classList.add('was-validated');
                return;
            }
        });
        form.querySelector('[name="ConfirmPassword"]')?.addEventListener('input', event => {
            const password = form.querySelector('[name="Password"]');
            event.target.setCustomValidity(password.value !== event.target.value ? 'Passwords do not match.' : '');
        });
    });

    document.querySelectorAll('.password-toggle').forEach(toggle => {
        toggle.addEventListener('click', () => {
            const input = toggle.closest('.password-wrap').querySelector('input');
            const isPassword = input.type === 'password';
            input.type = isPassword ? 'text' : 'password';
            toggle.innerHTML = `<i class="bi bi-eye${isPassword ? '-slash' : ''}"></i>`;
            toggle.setAttribute('aria-label', isPassword ? 'Hide password' : 'Show password');
        });
    });

    const moodKey = 'mindcare-demo-mood-date';
    const todayKey = new Date().toISOString().slice(0, 10);
    const savedMood = sessionStorage.getItem(moodKey);
    const applyMood = mood => {
        document.querySelectorAll('.mood-picker button').forEach(item => item.classList.toggle('selected', item.dataset.mood === mood));
        const message = document.querySelector('.mood-message');
        if (message) message.innerHTML = `<i class="bi bi-check-circle-fill me-1"></i> Mood recorded for today: <strong>${mood}</strong>.`;
        document.querySelector('.mood-recorded')?.classList.remove('d-none');
    };
    if (savedMood?.startsWith(`${todayKey}|`)) applyMood(savedMood.split('|')[1]);
    document.querySelectorAll('.mood-picker button').forEach(button => {
        button.addEventListener('click', () => {
            applyMood(button.dataset.mood);
            sessionStorage.setItem(moodKey, `${todayKey}|${button.dataset.mood}`);
        });
    });

    const noAssessments = document.getElementById('noAssessmentsState');
    if (noAssessments && new URLSearchParams(window.location.search).get('empty') === 'true') {
        noAssessments.classList.remove('d-none');
        document.querySelectorAll('.dashboard-wrap .summary-card, .dashboard-wrap .child-card, .dashboard-wrap .insight-card').forEach(element => element.classList.add('d-none'));
    }
});
