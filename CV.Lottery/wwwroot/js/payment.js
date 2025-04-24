// payment.js: Stripe frontend logic
let stripe = Stripe('pk_test_51OYWzaIWs7zkXRNzf0n4RnXJ6tuAYl61jqzZRDiXmahLkZGIY5f5t9ycywZTSfopSa1AEW8OHnCOqso4brHxnCQ700kmqPvAEO'); // TODO: Replace with your public key
let elements = stripe.elements();
let card = elements.create('card', {
    hidePostalCode: true,
    style: {
        base: {
            fontSize: '17px',
            color: '#212529',
            '::placeholder': { color: '#adb5bd' },
            fontFamily: 'inherit',
            iconColor: '#0d6efd',
            backgroundColor: '#f8f9fa',
        },
        invalid: {
            color: '#dc3545',
            iconColor: '#dc3545',
        }
    }
});
card.mount('#card-element');

// Enable amount field and submit button when card is valid
let cardComplete = false;

card.on('change', function(event) {
    const submitBtn = document.getElementById('submit-payment');
    if (event.complete && !event.error) {
        cardComplete = true;
        submitBtn.disabled = false;
    } else {
        cardComplete = false;
        submitBtn.disabled = true;
    }
});

let form = document.getElementById('payment-form');
form.addEventListener('submit', async function (e) {
    e.preventDefault();
    if (!cardComplete) {
        document.getElementById('card-errors').textContent = 'Please enter valid card details.';
        return;
    }
    const amountInput = document.getElementById('payment-amount');
    const amount = parseFloat(amountInput ? amountInput.value : 0);
    if (isNaN(amount) || amount <= 0) {
        document.getElementById('card-errors').textContent = 'Please enter a valid amount.';
        return;
    }
    document.getElementById('submit-payment').disabled = true;
    document.getElementById('card-errors').textContent = '';

    const {paymentMethod, error} = await stripe.createPaymentMethod({
        type: 'card',
        card: card,
    });

    if (error) {
        document.getElementById('card-errors').textContent = error.message;
        document.getElementById('submit-payment').disabled = false;
        return;
    }

    const userId = document.getElementById('user-id')?.value || '';
    // Submit paymentMethod.id to server
    let response = await fetch(window.location.pathname, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
        },
        body: JSON.stringify({ paymentMethodId: paymentMethod.id, userId, amount: amount.toString() })
    });
    let result = await response.json();
    if (result.redirect) {
        window.location = result.redirect;
    } else if (result.error) {
        document.getElementById('card-errors').textContent = result.error;
        document.getElementById('submit-payment').disabled = false;
    } else {
        window.location.reload();
    }
});
