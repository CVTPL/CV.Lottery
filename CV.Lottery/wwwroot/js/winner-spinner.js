let spinnerInterval = null;
let countdownInterval = null;
let spinning = false;
let participants = [];
let winner = null;

// Called from Razor page with participant data
function startDraw(participantList) {
    participants = participantList;
    winner = null;
    document.getElementById('winner-panel').style.display = 'none';
    document.getElementById('confetti').style.display = 'none';
    document.getElementById('draw-btn').disabled = true;
    document.getElementById('fireworks').style.display = 'none';
    startCountdown(3);
}

function startCountdown(seconds) {
    let timer = seconds;
    document.getElementById('countdown-timer').innerText = `Start draw in ${timer}...`;
    countdownInterval = setInterval(() => {
        timer--;
        document.getElementById('countdown-timer').innerText = `Start draw in ${timer}...`;
        if (timer <= 0) {
            clearInterval(countdownInterval);
            document.getElementById('countdown-timer').innerText = '';
            startSpinner();
        }
    }, 1000);
}

function startSpinner() {
    spinning = true;
    let spinnerList = document.getElementById('spinner-names');
    let displayCount = 8;
    // Corrected index logic: index points to the FIRST visible user in the list
    let index = Math.floor(Math.random() * participants.length);
    let spinSpeed = 60; // ms
    let spins = 0;
    let maxSpins = 40 + Math.floor(Math.random() * 20);
    let activeIdx = 3; // The highlighted index (center)
    let currentIndex = index; // Track the current starting index
    spinnerInterval = setInterval(() => {
        let names = [];
        for (let i = 0; i < displayCount; i++) {
            let idx;
            if (participants.length >= displayCount) {
                // Usual spinning logic: rotate through all participants
                idx = (currentIndex + i) % participants.length;
                names.push(`<div class='spinner-name${i === activeIdx ? ' active' : ''}' data-user-idx='${idx}'>${participants[idx].UserName}</div>`);
            } else {
                // For less than displayCount, fill top with empty, then actual names centered, then empty at bottom
                if (i < Math.floor((displayCount - participants.length) / 2) || i >= Math.floor((displayCount - participants.length) / 2) + participants.length) {
                    names.push(`<div class='spinner-name'>&nbsp;</div>`);
                } else {
                    // Center the actual participants
                    idx = (currentIndex + i - Math.floor((displayCount - participants.length) / 2)) % participants.length;
                    if (idx < 0) idx += participants.length;
                    names.push(`<div class='spinner-name${i === activeIdx ? ' active' : ''}' data-user-idx='${idx}'>${participants[idx].UserName}</div>`);
                }
            }
        }
        spinnerList.innerHTML = names.join('');
        currentIndex = (currentIndex + 1) % participants.length;
        spins++;
        if (spins > maxSpins) {
            clearInterval(spinnerInterval);
            spinning = false;
            // Winner is the user in the center (activeIdx) of the LAST rendered list
            let winnerIdx = (currentIndex + activeIdx - 1 + participants.length) % participants.length;
            winner = participants[winnerIdx];
            setTimeout(showWinnerAnimated, 400);
        }
        if (spins > maxSpins - 10) spinSpeed += 20; // slow down
    }, spinSpeed);
}

function showWinnerAnimated() {
    console.log('Winner object:', winner); // Debug: log the winner object
    document.getElementById('winner-name').innerText = winner.UserName || winner.userName || winner.name || 'N/A';
    document.getElementById('winner-email').innerText = winner.Email || winner.email || '';
    if (winner.AvatarUrl) {
        document.getElementById('winner-avatar').src = winner.AvatarUrl;
        document.getElementById('winner-avatar').style.display = 'block';
    } else {
        document.getElementById('winner-avatar').style.display = 'none';
    }
    document.getElementById('winner-panel').style.display = 'flex';
    document.getElementById('winner-panel').style.opacity = 0;
    setTimeout(() => {
        document.getElementById('winner-panel').style.opacity = 1;
        document.getElementById('winner-panel').style.animation = 'winnerReveal 1.2s cubic-bezier(0.23,1,0.32,1)';
        showFireworksOnWinnerPanel();
    }, 100);
    document.getElementById('confetti').style.display = 'block';
    confettiEffect();
    document.getElementById('draw-btn').disabled = false;
    showFireworksAndSound();
    // Set hidden winner input for saving
    let winnerInput = document.getElementById('WinnerUserIdInput');
    let winnerId = winner.UserId || winner.userId || winner.Id || winner.id || '';
    if (winnerInput) {
        winnerInput.value = winnerId;
        console.log('Setting WinnerUserIdInput value to:', winnerId); // Debug
    }
}

function showFireworksOnWinnerPanel() {
    // Place fireworks over the winner panel only
    let panel = document.getElementById('winner-panel');
    if (!panel) return;
    let fireworks = document.createElement('div');
    fireworks.className = 'fireworks';
    fireworks.style.position = 'absolute';
    fireworks.style.left = 0;
    fireworks.style.top = 0;
    fireworks.style.width = '100%';
    fireworks.style.height = '100%';
    fireworks.style.display = 'block';
    panel.appendChild(fireworks);
    for (let i = 0; i < 8; i++) {
        let fw = document.createElement('div');
        fw.className = 'firework';
        fw.style.left = (30 + Math.random()*40) + '%';
        fw.style.top = (30 + Math.random()*40) + '%';
        fw.style.background = `hsl(${Math.random()*360},90%,60%)`;
        fireworks.appendChild(fw);
    }
    setTimeout(() => { if (fireworks && fireworks.parentNode) fireworks.parentNode.removeChild(fireworks); }, 1800);
}

function replayDraw() {
    if (!participants.length) return;
    startDraw(participants);
}

// ---- Multi-block spinner grid logic ----
function startGridDraw(participantList) {
    participants = participantList;
    winner = null;
    document.getElementById('winner-panel').style.display = 'none';
    document.getElementById('confetti').style.display = 'none';
    document.getElementById('draw-btn').disabled = true;
    document.getElementById('fireworks').style.display = 'none';
    // Remove any focus classes
    let blocks = document.querySelectorAll('.spinner-block');
    blocks.forEach(b => b.classList.remove('focus'));
    startGridCountdown(3);
}

function startGridCountdown(seconds) {
    let timer = seconds;
    document.getElementById('countdown-timer').innerText = `Start draw in ${timer}...`;
    countdownInterval = setInterval(() => {
        timer--;
        document.getElementById('countdown-timer').innerText = `Start draw in ${timer}...`;
        if (timer <= 0) {
            clearInterval(countdownInterval);
            document.getElementById('countdown-timer').innerText = '';
            startGridSpinner();
        }
    }, 1000);
}

function startGridSpinner() {
    spinning = true;
    let blocks = document.querySelectorAll('.spinner-block');
    let indices = Array.from(Array(blocks.length).keys());
    let focusIdx = Math.floor(Math.random() * blocks.length);
    let spins = 0;
    let maxSpins = 30 + Math.floor(Math.random() * 18);
    let spinSpeed = 50;
    let prevIdx = null;
    spinnerInterval = setInterval(() => {
        if (prevIdx !== null) blocks[prevIdx].classList.remove('focus');
        focusIdx = Math.floor(Math.random() * blocks.length);
        blocks[focusIdx].classList.add('focus');
        prevIdx = focusIdx;
        spins++;
        if (spins > maxSpins) {
            clearInterval(spinnerInterval);
            spinning = false;
            // Select winner (focused block)
            blocks[focusIdx].classList.add('focus');
            winner = participants[focusIdx];
            setTimeout(showWinnerAnimated, 600);
        }
        if (spins > maxSpins - 8) spinSpeed += 30;
    }, spinSpeed);
}

function shareWin() {
    if (!winner) return;
    const shareText = ` ${winner.UserName} just won the lottery!`;
    if (navigator.share) {
        navigator.share({ title: 'Winner!', text: shareText });
    } else {
        alert(shareText);
    }
}

function confettiEffect() {
    // Simple confetti animation
    let confetti = document.getElementById('confetti');
    confetti.innerHTML = '';
    for (let i = 0; i < 80; i++) {
        let c = document.createElement('div');
        c.className = 'confetti-piece';
        c.style.left = Math.random() * 100 + '%';
        c.style.background = `hsl(${Math.random()*360},90%,60%)`;
        c.style.animationDuration = (1.5 + Math.random()*1.5) + 's';
        confetti.appendChild(c);
    }
}
