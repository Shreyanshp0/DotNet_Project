// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
document.addEventListener('DOMContentLoaded', () => {
    const roleKey = 'mindcare-demo-role';
    const storedRole = sessionStorage.getItem(roleKey);
    const guestActions = document.querySelector('.nav-actions-guest');
    const userActions = document.querySelector('.nav-actions-user');
    const roleLabel = document.querySelector('.nav-role-label');

    document.querySelectorAll('.nav-role').forEach(item => {
        item.classList.toggle('d-none', !storedRole || !item.classList.contains(`nav-role-${storedRole.toLowerCase()}`));
    });
    if (guestActions && userActions) {
        guestActions.classList.toggle('d-none', Boolean(storedRole));
        userActions.classList.toggle('d-none', !storedRole);
        userActions.classList.toggle('d-flex', Boolean(storedRole));
        if (roleLabel && storedRole) roleLabel.textContent = `${storedRole} preview`;
    }

    document.querySelector('.nav-logout')?.addEventListener('click', () => {
        sessionStorage.removeItem(roleKey);
        window.location.href = '/';
    });

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
            event.preventDefault();
            const selectedRole = form.querySelector('[name="Role"]:checked')?.value;
            if (form.dataset.accountForm && selectedRole) {
                sessionStorage.setItem(roleKey, selectedRole);
                const target = selectedRole === 'Parent' ? '/Parent/Index' : '/Student/Index';
                window.location.href = target;
                return;
            }
            const button = form.querySelector('button[type="submit"]');
            if (button) {
                button.innerHTML = '<i class="bi bi-check2 me-2"></i> Ready to connect';
                button.disabled = true;
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

    const roleInputs = document.querySelectorAll('[name="Role"]');
    const studentFields = document.getElementById('studentProfileFields');
    const parentFields = document.getElementById('parentLinkFields');
    const updateRoleFields = () => {
        const role = document.querySelector('[name="Role"]:checked')?.value;
        studentFields?.classList.toggle('d-none', role !== 'Student');
        parentFields?.classList.toggle('d-none', role !== 'Parent');
        if (studentFields) studentFields.querySelectorAll('input').forEach(input => input.required = role === 'Student' && input.id === 'dateOfBirth');
    };
    roleInputs.forEach(input => input.addEventListener('change', updateRoleFields));
    updateRoleFields();

    document.getElementById('sendInviteButton')?.addEventListener('click', () => {
        const email = document.querySelector('[name="ChildEmail"]');
        const message = document.getElementById('inviteMessage');
        if (!email?.checkValidity()) {
            email?.focus();
            if (message) message.textContent = 'Enter a valid child email address first.';
            return;
        }
        if (message) message.textContent = 'Invite ready — it will be sent when account services are connected.';
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
