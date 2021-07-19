async handleQueue() {
	if (this.queue.length > 0) {
		while (this.queue.length > 0) {
			const hub = await this.hubConnection;
			if (hub.state !== HubConnectionState.Connected) {
				break;
			}
			console.debug(`process {{typeName}} queue item`);
			const fn = this.queue[0];
			fn(undefined);
			this.queue.splice(0, 1);
		}
	}
	window.setTimeout(() => this.handleQueue(), this.OFFLINE_QUEUE_INTERVAL_SECONDS * 1000);
}
