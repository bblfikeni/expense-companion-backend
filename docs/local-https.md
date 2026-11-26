# Local HTTPS development

Use the HTTPS dev server for receipt capture and PWA testing on your computer and on phones connected to the same local network. Run the frontend commands from `web/`.

## Prerequisites

- Bun and the Node.js version required by the repository's Vite installation (Node.js 20.19+ or 22.12+).
- [mkcert](https://github.com/FiloSottile/mkcert#installation). On macOS, run `brew install mkcert`. The mkcert installation guide covers Linux and Windows.
- An available local TCP port (Vite starts at 8080) and a network that allows the phone to connect to your development machine.

## Generate certificates and start Vite

```sh
cd web
bun install --frozen-lockfile
bun run dev:mobile
```

Run `bun run dev:mobile` from the VS Code terminal. It runs `setup:https` and then starts `dev:https`. Use the **Network** HTTPS URL printed by Vite, which includes the port it selected.

Setup installs the mkcert root in the development machine's trust store and generates the server certificate and private key in the ignored `web/.certs/` directory. Accept the OS trust-store prompt in a terminal if prompted. A valid certificate is reused when it covers the requested addresses and matches the current CA and key.

Setup includes `localhost`, `127.0.0.1`, and the machine's detected LAN IPv4 addresses. To select the network address explicitly, run the following and restart the HTTPS server. Repeat `--ip` to specify more than one address.

```sh
bun run setup:https --ip 192.168.1.20
```

Open [https://localhost:8080/capture/image](https://localhost:8080/capture/image) or [https://127.0.0.1:8080/capture/image](https://127.0.0.1:8080/capture/image) on the development machine. The HTTPS server listens on all IPv4 interfaces. Vite uses a secure WebSocket on the same host and port for hot updates. Stop the HTTPS server before switching back to `bun run dev` over HTTP.

## Find your LAN address

Use the address of the network interface shared with the phone.

| System | Where to look |
| --- | --- |
| macOS | System Settings → Network → Wi-Fi → Details → TCP/IP, or run `ipconfig getifaddr en0` when Wi-Fi uses `en0`. |
| Windows | Run `ipconfig` and find the IPv4 address under the active Wi-Fi or Ethernet adapter. |
| Linux | Run `ip -4 addr` and find the address under the active Wi-Fi or Ethernet interface. |

## Trust the public CA certificate

`mkcert -CAROOT` prints the directory containing the public `rootCA.pem` file. Transfer only that public certificate to test devices. Keep `rootCA-key.pem` in the mkcert CA directory on the development machine and keep the server private key in `web/.certs/`.

**macOS.** `bun run setup:https` calls `mkcert -install`. If the terminal cannot install in the system trust store, run setup from an interactive terminal. To install the public CA in your macOS login keychain for SSL instead, run the following, then rerun setup and reopen the browser.

```sh
security add-trusted-cert -r trustRoot -p ssl \
  -k "$HOME/Library/Keychains/login.keychain-db" "$(mkcert -CAROOT)/rootCA.pem"
```

**Android.** Transfer the public certificate to the phone. If the file picker requires a certificate extension, save the copy with a `.crt` extension. In Settings, search for **Install a certificate** and choose **CA certificate** under **Encryption & credentials**. Select the copied certificate, confirm installation, and open the page in the browser. Menu names and user-CA policies vary by device. [Google's certificate installation instructions](https://support.google.com/pixelphone/answer/2844832?hl=en)

**iOS.** AirDrop the public certificate to the phone. If iOS does not open the PEM file, export a public DER copy with the command below and AirDrop the `.cer` file. Install the downloaded profile from Settings, then open **Settings → General → About → Certificate Trust Settings** and enable full trust for the mkcert root. [Apple's certificate trust instructions](https://support.apple.com/en-ie/102390)

```sh
openssl x509 -in "$(mkcert -CAROOT)/rootCA.pem" -outform der -out /tmp/companion-dev-ca.cer
```

## Open the app on a phone

Connect both devices to the same Wi-Fi. After trusting the public CA on the phone, open the **Network** HTTPS URL printed by Vite and go to `/capture/image`. For example, use `https://192.168.1.20:8080/capture/image` if that is your machine's IP and port. Tap **Take photo** and allow camera access. For an iOS standalone test, add the trusted LAN origin to the Home Screen from Safari and open the installed icon.

## Backend and troubleshooting

- **Mock mode.** Leave `VITE_COMPANION_API_URL` empty. This retains the existing mock auth and receipt upload setup.
- **Mixed content.** For live local authentication, set `VITE_COMPANION_API_URL=http://localhost:5101` in `web/.env.local` or in the dev server's environment. In HTTPS dev mode Vite sends browser API calls to the same HTTPS origin and forwards them to the configured HTTP gateway. Run the gateway on the development machine. Requests retain their original `/api/…` path at the gateway. An HTTPS gateway URL continues to be called directly and must allow the frontend origin in its CORS policy. Restart the dev server after changing the variable.
- **Certificate warning.** Confirm the device trusts the CA that issued the server certificate and that the URL host is covered by the certificate. If your IP changed, stop Vite and run `bun run dev:mobile` again to refresh the certificate. If trust installation is blocked by OS or device policy, use a device where you can install the test CA.
- **Camera permission.** Open the HTTPS page and check `window.isSecureContext` and `typeof navigator.mediaDevices?.getUserMedia` in the browser console. If permission was denied, allow camera access in the site's browser settings and in OS privacy settings, then reload.
- **Firewall or network.** If the computer can open the LAN URL but the phone cannot, check that the TCP port in the printed URL is allowed through the development machine's firewall. Guest Wi-Fi client isolation, VPNs and mismatched Wi-Fi networks can block access.
- **Hot updates.** Open Developer Tools → Network → WS to check for a `wss://` connection to the same host and port. A reverse proxy must forward WebSocket upgrades and preserve certificate trust.
