async invoke<T>(
	fn: () => Promise<T>,
	hub: HubConnection,
	name: string
): Promise<T> {
	if (hub.state === HubConnectionState.Connected) {
		return fn();
	} else {
		if (this.queue.length >= this.MAX_QUEUE_COUNT) {
			return Promise.reject(
				`Failed to add action to {{typeName}}.${name} queue.  Limited to ${
					this.MAX_QUEUE_COUNT
				} calls.`
			);
		}
		console.debug(`{{typeName}}.${name} not connected - adding to queue`);
		let resolver: PromiseResolver<unknown> | undefined;
		let rejector: PromiseRejector | undefined;
		const queueFn = new Promise((resolve, reject) => {
			resolver = resolve;
			rejector = reject;
		}).then(() => {
			return fn();
		});

		const timeout = new Promise<T>((_, reject) => {
			const id = window.setTimeout(() => {
				window.clearTimeout(id);
				const error = `{{typeName}}.${name} queue item timed out in ${
					this.QUEUE_TIMEOUT_SECONDS
				} seconds.`;
				reject(error);
				if (rejector) {
					rejector(error);
				}
			}, this.QUEUE_TIMEOUT_SECONDS * 1000);
		});

		if (resolver) {
			this.queue.push(resolver);
		}

		return Promise.race([queueFn, timeout]);
	}
}
