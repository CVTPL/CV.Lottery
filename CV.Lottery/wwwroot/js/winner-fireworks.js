// Fireworks and Winner Sound for Winner Panel
function showFireworksAndSound() {
    // Fireworks
    let fireworks = document.getElementById('fireworks');
    if (!fireworks) {
        fireworks = document.createElement('div');
        fireworks.id = 'fireworks';
        fireworks.className = 'fireworks';
        document.body.appendChild(fireworks);
    }
    fireworks.innerHTML = '';
    fireworks.style.display = 'block';
    for (let i = 0; i < 12; i++) {
        let fw = document.createElement('div');
        fw.className = 'firework';
        const angle = (i / 12) * 2 * Math.PI;
        const radius = 180 + Math.random()*60;
        fw.style.left = (50 + Math.cos(angle)*radius/10) + 'vw';
        fw.style.top = (50 + Math.sin(angle)*radius/18) + 'vh';
        fw.style.background = `hsl(${Math.random()*360},90%,60%)`;
        fireworks.appendChild(fw);
    }
    setTimeout(() => { fireworks.style.display = 'none'; }, 1800);
    // Winner Sound
    let audio = document.getElementById('winner-audio');
    if (!audio) {
        audio = document.createElement('audio');
        audio.id = 'winner-audio';
        audio.src = '/media/winner.mp3';
        document.body.appendChild(audio);
    }
    audio.currentTime = 0;
    audio.play();
}
