import { useCallback, useEffect, useState } from 'react';
import type { Worker } from '../types';
import {
  activateWorker,
  addWorker,
  deactivateWorker,
  deleteWorker,
  getAdminWorkers,
  setWorkerAdmin,
  triggerScheduleGeneration,
} from '../services/api';

export function AdminPage() {
  const [workers, setWorkers] = useState<Worker[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [newName, setNewName] = useState('');
  const [newEmail, setNewEmail] = useState('');
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);

  const loadWorkers = useCallback(async () => {
    try {
      const data = await getAdminWorkers();
      setWorkers(data);
      setError(null);
    } catch (err) {
      setError('Failed to load workers');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadWorkers();
  }, [loadWorkers]);

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
      await loadWorkers();
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
      await loadWorkers();
    } catch {
      showMessage('error', 'Failed to update worker status');
    }
  };

  const handleToggleAdmin = async (worker: Worker) => {
    try {
      await setWorkerAdmin(worker.id, !worker.isAdmin);
      showMessage('success', `Worker ${worker.isAdmin ? 'demoted' : 'promoted'} successfully`);
      await loadWorkers();
    } catch {
      showMessage('error', 'Failed to update worker role');
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteWorker(id);
      setConfirmDeleteId(null);
      showMessage('success', 'Worker deleted');
      await loadWorkers();
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

  if (isLoading) {
    return <div className="loading">Loading workers...</div>;
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
    </div>
  );
}
