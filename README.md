# SIGGEN – SCPI Signal Generator Control and Peak Scanner

**SIGGEN** is a Windows Forms application built in C# to control SCPI-compatible signal generators or spectrum analyzers over LAN (TCP port 5025). It supports real-time peak scanning, auto-tuning to the strongest frequency, and plotting using ScottPlot.

---

## 🧩 Features

- 📡 Connect to signal generator via IP (SCPI over TCP/IP)
- 📶 Set frequency sweep: Start, Stop, Step, Center
- 📊 Real-time plotting of frequency vs. peak power (dBm)
- 🔍 Auto-tune: Finds and sets best peak frequency
- 🧠 SCPI command console (send & read responses)
- 📈 Marker tracking (peak value and frequency)

---

## 🖥️ UI Overview

- **IP Address** input and Connect/Disconnect button
- **Frequency Configuration**: Start, Stop, Step, Center + unit selector (Hz/kHz/MHz/GHz)
- **Apply** button to confirm sweep parameters
- **Auto Tune** to find strongest frequency
- **SCPI Command** textbox with SEND button
- **Live Graph** showing peak power vs. frequency
- **Peak dBm** and **Marker Frequency** display
- **Clear Graph** to reset data

---

## 🛠 Requirements

- **.NET Framework 4.8** or **.NET 6+**
- **ScottPlot.WinForms** NuGet package
- A SCPI-compatible instrument (e.g., spectrum analyzer or RF signal generator) supporting:
  - `:FREQ`
  - `:CALC:MARK1:MAX`
  - `:CALC:MARK1:Y?`
  - `:CALC:MARK1:X?`

---
