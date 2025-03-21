const { invoke } = window.__TAURI__.core;

window.addEventListener('DOMContentLoaded', async () => {
    const button = document.getElementById('test');
    const wsUrl = await invoke('get_ws_url');
    let ws = null;

    button.addEventListener('click', async () => {
        if (ws) {
            ws.close();
        }

        ws = new WebSocket(wsUrl);
        
        ws.onmessage = (event) => {
            const stats = JSON.parse(event.data);
            updateStatsCard(stats, false, wsUrl);
            ws.close();
            ws = null;
        };

        ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            updateStatsCard(null, true, wsUrl);
            ws.close();
            ws = null;
        };
    });
});


function updateStatsCard(stats, error = false, wsUrl = '') {  // Add wsUrl parameter
    const content = document.querySelector('.content');
    
    if (error || !stats) {
        content.innerHTML = `
            <div style="color: red;">
                Connection Error
            </div>`;
        return;
    }

    content.innerHTML = `
        <div style="color: white; margin: 5px 0;">
            WebSocket: ${wsUrl}<br>
            CPU | Usage: ${stats.cpu_name ? '✅' : '❌'} ${stats.cpu_usage !== undefined ? '✅' : '❌'}<br>
            RAM | Usage: ${stats.ram_amount ? '✅' : '❌'} ${stats.ram_usage !== undefined ? '✅' : '❌'}<br>
            GPU | Usage: ${stats.gpu_name !== 'No NVIDIA GPU detected' ? '✅' : '❌'} ${stats.gpu_usage !== undefined ? '✅' : '❌'}<br>
        </div>`;
}

