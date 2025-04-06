const { invoke } = window.__TAURI__.core;
const { Window } = window.__TAURI__.window;
const { WebviewWindow } = window.__TAURI__.webviewWindow ;

// create a new window
window.addEventListener('DOMContentLoaded', async () => {
    const button = document.getElementById('test');
    const wsUrl = await invoke('get_ws_url');
    let ws = null;
    let isConnecting = false;
    initWebSocket();

    button.addEventListener('click', async () => {

        // Prevent multiple connections while connecting
        if (isConnecting) return;

        // Close existing connection if any
        if (ws && ws.readyState === WebSocket.OPEN) {
            ws.close();
            ws = null;
            return;
        }

        try {
            isConnecting = true;
            ws = new WebSocket(wsUrl);

            ws.onmessage = (event) => {
                const stats = JSON.parse(event.data);
                updateStatsCard(stats, false, wsUrl);
            };

            ws.onerror = (error) => {
                console.error('WebSocket error:', error);
                updateStatsCard(null, true, wsUrl);
                ws.close();
                ws = null;
                isConnecting = false;
            };

            ws.onclose = () => {
                updateStatsCard(null, true, wsUrl);
                ws = null;
                isConnecting = false;
            };

            ws.onopen = () => {
                isConnecting = false;
            };

        } catch (error) {
            console.error('Failed to connect:', error);
            updateStatsCard(null, true, wsUrl);
            isConnecting = false;
        }
    });

    document.getElementById('stats').addEventListener('click', () => {

        const webview = new WebviewWindow('stats', {
            url: 'stats.html',
            title: 'Stats',
            width: 500,
            height: 45,
            resizable: false,
            decorations: false,
            focus: true,
            alwaysOnTop: true,
            transparent: true,
            y: 10,
            x: Math.floor((window.screen.width - 400) / 2),
            skipTaskbar: false,
            shadow: false
        });

        webview.once('tauri://created', () => {
            console.log('New window created successfully!');
        });

        webview.once('tauri://error', (e) => {
            console.error('Error creating window:', e);
        });
    });

});

function updateStatsCard(stats, error = false, wsUrl = '') {
    const content = document.querySelector('.content');

    if (error || !stats) {
        content.innerHTML = `
            <table>
                <tbody>
                    <tr>
                        <th scope="row">Status</th>
                        <td style="color: red;">Connection Error</td>
                    </tr>
                    <tr>
                        <th scope="row">WebSocket</th>
                        <td style="color: red;">${wsUrl}</td>
                    </tr>
                </tbody>
            </table>`;
        return;
    }

    content.innerHTML = `
    <table>
        <tbody>
           <tr>
                <th scope="row">WebSocket</th>
                <td>${!error ? `✅ ${stats.active_connections} ${wsUrl}` : `❌ ${wsUrl}`}</td>
            </tr>
            <tr>
                <th scope="row">CPU</th>
                <td>${stats.cpu_name ? '✅' : '❌'} ${stats.cpu_usage !== undefined ? '✅' : '❌'}</td>
            </tr>
            <tr>
                <th scope="row">RAM</th>
                <td>${stats.ram_amount ? '✅' : '❌'} ${stats.ram_usage !== undefined ? '✅' : '❌'}</td>
            </tr>
            <tr>
                <th scope="row">GPU</th>
                <td>${stats.gpu_name !== 'No NVIDIA GPU detected' ? '✅' : '❌'} ${stats.gpu_usage !== undefined ? '✅' : '❌'} ${stats.gpu_temp !== undefined ? '✅' : '❌'}</td>
            </tr>
            <tr>
                <th scope="row">Network</th>
                <td> ${stats.network_down !== undefined ? '✅' : '❌'} ${stats.network_up !== undefined ? '✅' : '❌'}</td>
            </tr>
            <tr>
                <th scope="row">Disk</th>
                <td> ${stats.disks !== undefined ? '✅' : '❌'}</td>
            </tr>
        </tbody>
    </table>`;
}

// Initialize WebSocket connection
async function initWebSocket() {
    try {
        const wsUrl = await invoke('get_ws_url');
        const ws = new WebSocket(wsUrl);

        ws.onmessage = (event) => {
            const stats = JSON.parse(event.data);
            if (document.getElementById('statsText')) {
                updateStats(stats);
            }
        };

        ws.onerror = (error) => {
            console.error('WebSocket error:', error);
        };

        ws.onclose = () => {
            console.log('WebSocket connection closed');
            // Attempt to reconnect after 5 seconds
            setTimeout(initWebSocket, 5000);
        };

    } catch (error) {
        console.error('Failed to connect:', error);
        // Attempt to reconnect after 5 seconds
        setTimeout(initWebSocket, 5000);
    }
}

// Function to update the stats display
function updateStats(stats) {
    // Check if elements exist before updating
    const gpuUsage = document.getElementById('gpu_usage');
    const gpuTemp = document.getElementById('gpu_temp');
    const cpuUsage = document.getElementById('cpu_usage');
    const ramUsage = document.getElementById('ram_usage');

    if (gpuUsage) gpuUsage.textContent = `${Math.round(stats.gpu_usage)}%`;
    if (gpuTemp) gpuTemp.textContent = `[${Math.round(stats.gpu_temp)} °C]`;
    if (cpuUsage) cpuUsage.textContent = `${Math.round(stats.cpu_usage)}%`;
    if (ramUsage) ramUsage.textContent = `${Math.round(stats.ram_usage)}%`;
}


document.addEventListener('DOMContentLoaded', () => {
    const colorPicker = document.getElementById('textColorPicker');
    if (colorPicker) {
        colorPicker.addEventListener('change', (e) => {
            const selectedColor = e.target.value;
            // Store the selected color in localStorage
            localStorage.setItem('statsTextColor', selectedColor);
        });
    }

    // Check if this is the stats page
    const statsText = document.getElementById('statsText');
    if (statsText) {
        // Apply the stored color if available
        const savedColor = localStorage.getItem('statsTextColor');
        if (savedColor) {
            statsText.style.color = savedColor;
        }

        // Listen for color changes
        window.addEventListener('storage', (e) => {
            if (e.key === 'statsTextColor') {
                statsText.style.color = e.newValue;
            }
        });
    }

    // Add background color picker functionality
    const bgColorPicker = document.getElementById('bgColorPicker');
    if (bgColorPicker) {
        bgColorPicker.addEventListener('change', (e) => {
            const selectedColor = e.target.value;
            localStorage.setItem('statsBackground', selectedColor);
        });
    }

    // Check if this is the stats page
    const statsCard = document.querySelector('.Statcard');
    if (statsCard) {
        // Apply the stored background if available
        const savedBackground = localStorage.getItem('statsBackground');
        if (savedBackground) {
            statsCard.style.background = savedBackground;
        }

        // Listen for background changes
        window.addEventListener('storage', (e) => {
            if (e.key === 'statsBackground') {
                statsCard.style.background = e.newValue;
            }
        });
    }
});
