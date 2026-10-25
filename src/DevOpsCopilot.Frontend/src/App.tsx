import './App.css';
import StatusDashboard from './StatusDashboard';

function App() {
  return (
    <div className="app-container">
      <header className="app-header">
        <h1 className="app-title">DevOpsCopilot</h1>
        <p className="app-subtitle">Worker status and processing statistics</p>
      </header>

      <main className="main-content">
        <StatusDashboard />
      </main>
    </div>
  );
}

export default App;
