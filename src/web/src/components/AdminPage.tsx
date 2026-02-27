import { useCallback, useEffect, useState } from 'react';
import type { BarnConfig, BlackoutDate, Worker } from '../types';
import { Barn, ShiftTime } from '../types';
import {
  activateWorker,
  addBlackout,
  addWorker,
  deactivateWorker,
  deleteBlackout,
  deleteWorker,
  getAdminWorkers,
  getBarnConfigs,
  getBlackouts,
  setBarnConfig,
  setWorkerAdmin,
  triggerScheduleGeneration,
} from '../services/api';

export function AdminPage() {
  const [workers, setWorkers] = useState<Worker[]>([]);
  const [barnConfigs, setBarnConfigs] = useState<BarnConfig[]>([]);
  const [blackouts, setBlackouts] = useState<BlackoutDate[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [newName, setNewName] = useState('');
  const [newEmail, setNewEmail] = useState('');
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);

  // Blackout form state
  const [newBlackoutDate, setNewBlackoutDate] = useState('');
  const [newBlackoutDesc, setNewBlackoutDesc] = useState('');
  const [newBlackoutBarn, setNewBlackoutBarn] = useState<string>('');
  const [newBlackoutShift, setNewBlackoutShift] = useState<string>('');

  const loadData = useCallback(async () => {
    try {
      const [workerData, barnData, blackoutData] = await Promise.all([
        getAdminWorkers(),
        getBarnConfigs(),
        getBlackouts(),
      ]);
      setWorkers(workerData);
      setBarnConfigs(barnData);
      setBlackouts(blackoutData);
      setError(null);
    } catch (err) {
      setError('Failed to load data');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const showMessage = (type: 'success' | 'error', text: string) => {
    setMessage({ type, text });
    setTimeout(() => setMessage(null), 3000);
  };

  const handleAddWorker = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newName.trim() || !newEmail.trim()) return;
    try {
      await addWorker(newName.trim(), newEmail.trim());
      setNewName('');
      setNewEmail('');
      showMessage('success', 'Worker added successfully');
      await loadData();
    } catch {
      showMessage('error', 'Failed to add worker');
    }
  };

  const handleToggleActive = async (worker: Worker) => {
    try {
      if (worker.isActive) {
        await deactivateWorker(worker.id);
      } else {
        await activateWorker(worker.id);
      }
      showMessage('success', `Worker ${worker.isActive ? 'deactivated' : 'activated'}`);
      await loadData();
    } catch {
      showMessage('error', 'Failed to update worker status');
    }
  };

  const handleToggleAdmin = async (worker: Worker) => {
    try {
      await setWorkerAdmin(worker.id, !worker.isAdmin);
      showMessage('success', `Worker ${worker.isAdmin ? 'demoted' : 'promoted'} successfully`);
      await loadData();
    } catch {
      showMessage('error', 'Failed to update worker role');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteWorker(id);
      setConfirmDeleteId(null);
      showMessage('success', 'Worker deleted');
      await loadData();
    } catch {
      showMessage('error', 'Failed to delete worker');
    }
  };

  const handleGenerateSchedule = async () => {
    setIsGenerating(true);
    try {
      await triggerScheduleGeneration();
      showMessage('success', 'Schedule generated and published successfully');
    } catch {
      showMessage('error', 'Failed to generate schedule');
    } finally {
      setIsGenerating(false);
    }
  };

  const handleBarnConfigChange = async (barn: string, workersPerShift: number) => {
    try {
      await setBarnConfig(barn, workersPerShift);
      showMessage('success', `${barn} updated to ${workersPerShift} worker(s) per shift`);
      await loadData();
    } catch {
      showMessage('error', 'Failed to update barn configuration');
    }
  };

  const handleAddBlackout = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newBlackoutDate) return;
    try {
      await addBlackout({
        date: newBlackoutDate,
        description: newBlackoutDesc,
        barn: newBlackoutBarn || null,
        shift: newBlackoutShift || null,
      });
      setNewBlackoutDate('');
      setNewBlackoutDesc('');
      setNewBlackoutBarn('');
      setNewBlackoutShift('');
      showMessage('success', 'Blackout date added');
      await loadData();
    } catch {
      showMessage('error', 'Failed to add blackout date');
    }
  };

  const handleDeleteBlackout = async (id: string) => {
    try {
      await deleteBlackout(id);
      showMessage('success', 'Blackout date removed');
      await loadData();
    } catch {
      showMessage('error', 'Failed to remove blackout date');
    }
  };

  if (isLoading) {
    return <div className="loading">Loading...</div>;
  }

  if (error) {
    return <div className="error-message">{error}</div>;
  }

  return (
    <div className="admin-page">
      <h2>Worker Management</h2>

      <form className="admin-add-form" onSubmit={handleAddWorker}>
        <input
          type="text"
          placeholder="Display name"
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
          className="admin-input"
          required
        />
        <input
          type="email"
          placeholder="Email"
          value={newEmail}
          onChange={(e) => setNewEmail(e.target.value)}
          className="admin-input"
          required
        />
        <button type="submit" className="admin-btn admin-btn-add">
          Add Worker
        </button>
      </form>

      <div className="admin-schedule-section">
        <button
          className="admin-btn admin-btn-generate"
          onClick={handleGenerateSchedule}
          disabled={isGenerating}
        >
          {isGenerating ? 'Generating...' : 'Generate Schedule Now'}
        </button>
      </div>

      {message && (
        <div className={`toast ${message.type === 'success' ? 'toast-success' : 'toast-error'}`}>
          {message.text}
        </div>
      )}

      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Status</th>
              <th>Role</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {workers.map((worker) => (
              <tr key={worker.id}>
                <td>{worker.displayName}</td>
                <td>{worker.email}</td>
                <td>
                  <span className={`admin-badge ${worker.isActive ? 'badge-active' : 'badge-inactive'}`}>
                    {worker.isActive ? 'Active' : 'Inactive'}
                  </span>
                </td>
                <td>
                  <span className={`admin-badge ${worker.isAdmin ? 'badge-admin' : 'badge-worker'}`}>
                    {worker.isAdmin ? 'Admin' : 'Worker'}
                  </span>
                </td>
                <td className="admin-actions">
                  <button
                    className={`admin-btn ${worker.isActive ? 'admin-btn-deactivate' : 'admin-btn-activate'}`}
                    onClick={() => handleToggleActive(worker)}
                  >
                    {worker.isActive ? 'Deactivate' : 'Activate'}
                  </button>
                  <button
                    className="admin-btn admin-btn-admin"
                    onClick={() => handleToggleAdmin(worker)}
                  >
                    {worker.isAdmin ? 'Remove Admin' : 'Make Admin'}
                  </button>
                  {confirmDeleteId === worker.id ? (
                    <>
                      <button
                        className="admin-btn admin-btn-delete"
                        onClick={() => handleDelete(worker.id)}
                      >
                        Confirm
                      </button>
                      <button
                        className="admin-btn"
                        onClick={() => setConfirmDeleteId(null)}
                      >
                        Cancel
                      </button>
                    </>
                  ) : (
                    <button
                      className="admin-btn admin-btn-delete"
                      onClick={() => setConfirmDeleteId(worker.id)}
                    >
                      Delete
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Barn Configuration */}
      <h2>Barn Configuration</h2>
      <div className="table-wrapper">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Barn</th>
              <th>Workers Per Shift</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {barnConfigs.map((config) => (
              <tr key={config.barn}>
                <td>{config.barn}</td>
                <td>{config.workersPerShift}</td>
                <td className="admin-actions">
                  <button
                    className="admin-btn"
                    onClick={() => handleBarnConfigChange(config.barn, config.workersPerShift - 1)}
                    disabled={config.workersPerShift <= 1}
                  >
                    −
                  </button>
                  <button
                    className="admin-btn"
                    onClick={() => handleBarnConfigChange(config.barn, config.workersPerShift + 1)}
                  >
                    +
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Blackout Dates */}
      <h2>Blackout Dates</h2>
      <form className="admin-add-form" onSubmit={handleAddBlackout}>
        <input
          type="date"
          value={newBlackoutDate}
          onChange={(e) => setNewBlackoutDate(e.target.value)}
          className="admin-input"
          required
        />
        <input
          type="text"
          placeholder="Description (e.g., Christmas Day)"
          value={newBlackoutDesc}
          onChange={(e) => setNewBlackoutDesc(e.target.value)}
          className="admin-input"
        />
        <select
          value={newBlackoutBarn}
          onChange={(e) => setNewBlackoutBarn(e.target.value)}
          className="admin-input"
        >
          <option value="">All Barns</option>
          {Object.values(Barn).map((b) => (
            <option key={b} value={b}>{b}</option>
          ))}
        </select>
        <select
          value={newBlackoutShift}
          onChange={(e) => setNewBlackoutShift(e.target.value)}
          className="admin-input"
        >
          <option value="">All Shifts</option>
          {Object.values(ShiftTime).map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <button type="submit" className="admin-btn admin-btn-add">
          Add Blackout
        </button>
      </form>
      {blackouts.length > 0 && (
        <div className="table-wrapper">
          <table className="admin-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Description</th>
                <th>Scope</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {blackouts.map((b) => (
                <tr key={b.id}>
                  <td>{b.date}</td>
                  <td>{b.description || '—'}</td>
                  <td>
                    {b.barn ? b.barn : 'All Barns'}
                    {' / '}
                    {b.shift ? b.shift : 'All Shifts'}
                  </td>
                  <td>
                    <button
                      className="admin-btn admin-btn-delete"
                      onClick={() => handleDeleteBlackout(b.id)}
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
