// BharatCMS IndexedDB wrapper for offline data storage

class BharatOfflineDB {
  constructor() {
    this.dbName = 'BharatCMS';
    this.version = 1;
    this.db = null;
  }

  async init() {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, this.version);
      
      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        this.db = request.result;
        resolve(this.db);
      };
      
      request.onupgradeneeded = (event) => {
        const db = event.target.result;
        
        // Sync queue for offline operations
        if (!db.objectStoreNames.contains('syncQueue')) {
          const syncStore = db.createObjectStore('syncQueue', { 
            keyPath: 'id', 
            autoIncrement: true 
          });
          syncStore.createIndex('status', 'status');
          syncStore.createIndex('createdAt', 'createdAt');
        }
        
        // Content cache
        if (!db.objectStoreNames.contains('content')) {
          const contentStore = db.createObjectStore('content', { keyPath: 'key' });
          contentStore.createIndex('type', 'type');
          contentStore.createIndex('updatedAt', 'updatedAt');
        }
        
        // Media files
        if (!db.objectStoreNames.contains('media')) {
          const mediaStore = db.createObjectStore('media', { keyPath: 'id' });
          mediaStore.createIndex('localPath', 'localPath');
        }
        
        // Form drafts
        if (!db.objectStoreNames.contains('drafts')) {
          db.createObjectStore('drafts', { keyPath: 'id', autoIncrement: true });
        }
      };
    });
  }

  // Sync queue operations
  async addToSyncQueue(operation) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('syncQueue', 'readwrite');
      const store = tx.objectStore('syncQueue');
      
      const item = {
        ...operation,
        status: 'pending',
        createdAt: new Date().toISOString(),
        retryCount: 0
      };
      
      const request = store.add(item);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  async getSyncQueue(status = null) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('syncQueue', 'readonly');
      const store = tx.objectStore('syncQueue');
      
      let request;
      if (status) {
        request = store.index('status').getAll(status);
      } else {
        request = store.getAll();
      }
      
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  async updateSyncItem(id, updates) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('syncQueue', 'readwrite');
      const store = tx.objectStore('syncQueue');
      
      const getRequest = store.get(id);
      getRequest.onsuccess = () => {
        const item = { ...getRequest.result, ...updates };
        const putRequest = store.put(item);
        putRequest.onsuccess = () => resolve(item);
        putRequest.onerror = () => reject(putRequest.error);
      };
      getRequest.onerror = () => reject(getRequest.error);
    });
  }

  async removeSyncItem(id) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('syncQueue', 'readwrite');
      const request = tx.objectStore('syncQueue').delete(id);
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  // Content cache operations
  async cacheContent(key, data, type = 'general') {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('content', 'readwrite');
      const store = tx.objectStore('content');
      
      const item = {
        key,
        type,
        data,
        updatedAt: new Date().toISOString()
      };
      
      const request = store.put(item);
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  async getCachedContent(key) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('content', 'readonly');
      const request = tx.objectStore('content').get(key);
      request.onsuccess = () => resolve(request.result?.data);
      request.onerror = () => reject(request.error);
    });
  }

  async clearStaleCache(maxAgeMs = 24 * 60 * 60 * 1000) {
    const db = await this.init();
    const cutoff = new Date(Date.now() - maxAgeMs).toISOString();
    
    return new Promise((resolve, reject) => {
      const tx = db.transaction('content', 'readwrite');
      const store = tx.objectStore('content');
      const index = store.index('updatedAt');
      
      const range = IDBKeyRange.upperBound(cutoff);
      const request = index.openCursor(range);
      
      let deleted = 0;
      request.onsuccess = () => {
        const cursor = request.result;
        if (cursor) {
          cursor.delete();
          deleted++;
          cursor.continue();
        } else {
          resolve(deleted);
        }
      };
      request.onerror = () => reject(request.error);
    });
  }

  // Media operations
  async saveMediaLocally(id, blob, metadata = {}) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('media', 'readwrite');
      const store = tx.objectStore('media');
      
      const item = {
        id,
        blob,
        metadata,
        localPath: `local://${id}`,
        savedAt: new Date().toISOString()
      };
      
      const request = store.put(item);
      request.onsuccess = () => resolve(item.localPath);
      request.onerror = () => reject(request.error);
    });
  }

  async getLocalMedia(id) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('media', 'readonly');
      const request = tx.objectStore('media').get(id);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }
}

// Form draft operations
class FormDraftManager {
  constructor() {
    this.db = null;
  }

  async init() {
    if (!this.db) {
      this.db = await new Promise((resolve, reject) => {
        const request = indexedDB.open('BharatCMS', 1);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => resolve(request.result);
      });
    }
    return this.db;
  }

  async saveDraft(formId, data) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('drafts', 'readwrite');
      const store = tx.objectStore('drafts');
      
      const item = {
        id: formId,
        data,
        savedAt: new Date().toISOString()
      };
      
      const request = store.put(item);
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  async getDraft(formId) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('drafts', 'readonly');
      const request = tx.objectStore('drafts').get(formId);
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  async deleteDraft(formId) {
    const db = await this.init();
    return new Promise((resolve, reject) => {
      const tx = db.transaction('drafts', 'readwrite');
      const request = tx.objectStore('drafts').delete(formId);
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }
}

// Export singleton instances
const offlineDB = new BharatOfflineDB();
const formDrafts = new FormDraftManager();
