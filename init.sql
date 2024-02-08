CREATE TABLE clientes (
	id SERIAL PRIMARY KEY,
	nome VARCHAR(50) NOT NULL, -- nem precisa mas ta aí
	limite INTEGER NOT NULL,
	saldo INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE transacoes (
	id SERIAL PRIMARY KEY,
	cliente_id INTEGER NOT NULL,
	valor INTEGER NOT NULL,
	tipo CHAR(1) NOT NULL,
	descricao VARCHAR(10) NOT NULL,
	realizada_em TIMESTAMP NOT NULL DEFAULT NOW(),
	CONSTRAINT fk_clientes_transacoes_id
		FOREIGN KEY (cliente_id) REFERENCES clientes(id)
);

CREATE TYPE resultado_transacao as (cliente_novo_saldo integer, cliente_limite integer);

CREATE FUNCTION criar_transacao(cliente_id INTEGER, valor_transacao INTEGER, tipo_transacao TEXT, descricao_transacao TEXT)
RETURNS SETOF resultado_transacao
LANGUAGE plpgsql
AS $BODY$
	DECLARE cliente_novo_saldo INTEGER;
  DECLARE cliente_limite INTEGER;
BEGIN
	update clientes
	set saldo = saldo + valor_transacao
	where id = cliente_id and saldo + valor_transacao >= (-limite)
	returning saldo, limite into cliente_novo_saldo, cliente_limite;

	if cliente_novo_saldo is null then return; end if;

	insert into transacoes (cliente_id, valor, tipo, descricao)
	values (cliente_id, abs(valor_transacao), tipo_transacao, descricao_transacao);

	return query select cliente_novo_saldo, cliente_limite;
END;
$BODY$;

INSERT INTO clientes (nome, limite) values
	('eles brigam', 1000 * 100),
	('por instinto', 800 * 100),
	('nós criamos', 10000 * 100),
	('por amor', 100000 * 100),
	('rinha de galo frases', 5000 * 100);
