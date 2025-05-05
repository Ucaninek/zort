function fetchClients() {
  fetch('/getClients')
    .then(res => {
      if (res.status === 401) {
        location.reload();
        return;
      }
      return res.json();
    })
    .then(data => {
      const container = document.getElementById('clients');
      container.innerHTML = '';

      for (const [hwid, client] of Object.entries(data)) {
        const sys = client.sysdata || {};
        const block = document.createElement('div');
        block.className = 'client';
        const timeInputId = `time-${hwid}`;

        const statusColor = client.isOnline ? 'green' : 'red';
        const statusText = client.isOnline ? 'Online' : 'Offline';

        block.innerHTML = `
          <h2>${sys.HostName || 'Unknown'} <span class="hwid">[${hwid}]</span></h2>
          <p id="os"><strong>OS:</strong> ${sys.OSName} (${sys.OSVersion})</p>
          <p id="manufacturer"><strong>Manufacturer:</strong> ${sys.SystemManufacturer}</p>
          <p id="model"><strong>Model:</strong> ${sys.SystemModel}</p>
          <p id="processor"><strong>Processor:</strong> ${sys.Processor}</p>
          <p id="gpu"><strong>GPU:</strong> ${sys.VideoCard}</p>
          <p id="bios"><strong>BIOS:</strong> ${sys.BIOSVersion}</p>
          <p id="user"><strong>User:</strong> ${sys.UserName}</p>
          <p id="totalRam"><strong>Total RAM:</strong> ${(sys.TotalPhysicalMemory / 1024 / 1024 / 1024).toFixed(2)} GB</p>
          <p id="lastBoot"><strong>Last Boot:</strong> ${sys.SystemBootTime}</p>
	  <p id="lastHeartbeat"><strong>Last Heartbeat:</strong> ${new Date(client.heartbeat).toLocaleString()}</p>
          <p id="status"><strong>Status:</strong> <span style="color:${statusColor}; font-weight: bold;">●</span> ${statusText}</p>
          <div class="fart-buttons">
            <button onclick="sendFart('${hwid}', 'classic')">Fart Now (Classic)</button>
            <button onclick="sendFart('${hwid}', 'puke')">Fart Now (Puke)</button><br>
            <input type="datetime-local" id="${timeInputId}" />
            <button onclick="scheduleFart('${hwid}', 'classic', '${timeInputId}')">Schedule Classic</button>
            <button onclick="scheduleFart('${hwid}', 'puke', '${timeInputId}')">Schedule Puke</button>
          </div>
        `;
        container.appendChild(block);
      }
    });
}

function sendFart(hwid, type) {
  fetch(`/fartNow?hwid=${encodeURIComponent(hwid)}&type=${encodeURIComponent(type)}`)
    .then(() => Toast.fire({
      icon: "success",
      title: "Fart sent successfully"
    }))
    .catch(() => Toast.fire({
      icon: "error",
      title: "Failed to send fart"
    }));
}

function getFriendlyName(hwid) {
  const hwidElement = Array.from(document.querySelectorAll('.client .hwid'))
    .find(el => el.textContent.includes(`[${hwid}]`));
  const block = hwidElement?.closest('.client');
  if (!block) return hwid;
  const nameElement = block.querySelector('h2');
  return nameElement ? nameElement.textContent.replace(/\s*\[.*?\]\s*/, '') : hwid;
}

function scheduleFart(hwid, type, inputId) {
  const input = document.getElementById(inputId);
  const time = input.value;
  if (!time) return Toast.fire({
    icon: "error",
    title: "Please select a time"
  });
  //get the timestamp in milliseconds
  const date = new Date(time);
  const timestamp = date.getTime();
  console.log(new Date(timestamp).toLocaleString());
  fetch(`/scheduleFart?hwid=${encodeURIComponent(hwid)}&type=${encodeURIComponent(type)}&timestamp=${timestamp}`)
    .then(() => Toast.fire({
      icon: "success",
      title: (`Scheduled ${type} fart for ${getFriendlyName(hwid)}`),
      text: `Fart will be sent at ${new Date(timestamp).toLocaleString()}`
    }))
    .catch(() => Toast.fire({
      icon: "error",
      title: (`Failed to schedule ${type} fart for ${hwid}`)
    }));
}


async function login() {
  const u = document.getElementById('username').value;
  const p = document.getElementById('password').value;

  const res = await fetch(`/auth?username=${encodeURIComponent(u)}&password=${encodeURIComponent(p)}`);
  const msg = document.getElementById('login-msg');

  if (res.ok) {
    document.getElementById('login-section').style.display = 'none';
    document.getElementById('clients').style.display = 'block';
    fetchClients();
    setInterval(updateClients, 5000);

    document.removeEventListener("keydown", function (event) {
      if (event.key === "Enter") {
        document.getElementById("login-button").click();
      }
    });
  } else {
    msg.textContent = 'Login failed';
  }
}

async function logout() {
  await fetch('/logout');
  location.reload();
}

const Toast = Swal.mixin({
  toast: true,
  position: "top-end",
  showConfirmButton: false,
  timer: 3000,
  timerProgressBar: true,
  didOpen: (toast) => {
    toast.onmouseenter = Swal.stopTimer;
    toast.onmouseleave = Swal.resumeTimer;
  }
});

async function updateClients() {
  fetch('/getClients')
    .then(res => {
      if (res.status === 401) {
        location.reload();
        return;
      }
      return res.json();
    })
    .then(data => {
      for (const [hwid, client] of Object.entries(data)) {
        const sys = client.sysdata || {};
        const hwidElement = Array.from(document.querySelectorAll('.client .hwid'))
          .find(el => el.textContent.includes(`[${hwid}]`));
        const block = hwidElement?.closest('.client');
        if (!block) continue;

        const statusColor = client.isOnline ? 'green' : 'red';
        const statusText = client.isOnline ? 'Online' : 'Offline';

        block.querySelector('h2').innerHTML = `${sys.HostName || 'Unknown'} <span class="hwid">[${hwid}]</span>`;
        block.querySelector('#os').innerHTML = `<strong>OS:</strong> ${sys.OSName} (${sys.OSVersion})`;
        block.querySelector('#manufacturer').innerHTML = `<strong>Manufacturer:</strong> ${sys.SystemManufacturer}`;
        block.querySelector('#model').innerHTML = `<strong>Model:</strong> ${sys.SystemModel}`;
        block.querySelector('#processor').innerHTML = `<strong>Processor:</strong> ${sys.Processor}`;
        block.querySelector('#gpu').innerHTML = `<strong>GPU:</strong> ${sys.VideoCard}`;
        block.querySelector('#bios').innerHTML = `<strong>BIOS:</strong> ${sys.BIOSVersion}`;
        block.querySelector('#user').innerHTML = `<strong>User:</strong> ${sys.UserName}`;
        block.querySelector('#totalRam').innerHTML = `<strong>Total RAM:</strong> ${(sys.TotalPhysicalMemory / 1024 / 1024 / 1024).toFixed(2)} GB`;
        block.querySelector('#lastBoot').innerHTML = `<strong>Last Boot:</strong> ${sys.SystemBootTime}`;
        block.querySelector('#lastHeartbeat').innerHTML = `<strong>Last Heartbeat:</strong> ${new Date(client.heartbeat).toLocaleString()}`;
        block.querySelector('#status').innerHTML = `<strong>Status:</strong> <span style="color:${statusColor}; font-weight: bold;">●</span> ${statusText}`;
      }
    });
}

// bypass login if /getClients gives 200
fetch('/getClients')
  .then(res => {
    if (res.status === 200) {
      document.getElementById('login-section').style.display = 'none';
      document.getElementById('clients').style.display = 'block';
      fetchClients();
      setInterval(updateClients, 5000);

      document.removeEventListener("keydown", function (event) {
        if (event.key === "Enter") {
          document.getElementById("login-button").click();
        }
      });
    }
  });