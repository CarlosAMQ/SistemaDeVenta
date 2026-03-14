let ValorImpuesto = 0;
let ProductosParaVenta = [];
let impuestoListo = false;

$(document).ready(function () {

    $("#txtDescuento").on("input", function () {
        mostrarProducto_Precios();
    });

    // ===============================
    // OBTENER DATOS DE NEGOCIO (Impuesto)
    // ===============================
    fetch("/Negocio/Obtener")
        .then(res => res.ok ? res.json() : Promise.reject(res))
        .then(resJson => {
            if (resJson.estado) {
                const d = resJson.objeto;

                ValorImpuesto = parseFloat(d.porcentajeImpuesto);
                impuestoListo = true;

                console.log("Impuesto cargado correctamente:", ValorImpuesto);
                activarSelectorProductos();
            } else {
                console.error("No se pudo cargar el impuesto.");
                impuestoListo = true;
                ValorImpuesto = 16;
                activarSelectorProductos();
            }
        });

    // ======================================
    // SELECT para buscar cliente
    // ======================================
    $("#cboBuscarCliente").select2({
        ajax: {
            url: "/Cliente/BuscarClientes",
            dataType: 'json',
            delay: 250,
            data: params => ({ busqueda: params.term }),
            processResults: function (data) {
                return {
                    results: data.map(item => ({
                        id: item.idCliente,
                        text: item.nombreCompleto,
                        correo: item.correo,
                        rfc: item.rfc,
                        domicilioFiscal: item.domicilioFiscalReceptor,
                        regimenFiscal: item.regimenFiscalReceptor,
                        fechaRegistro: item.fechaRegistro
                    }))
                };
            }
        },
        placeholder: "Buscar Cliente...",
        minimumInputLength: 1,
        language: "es"
    });

    $("#cboBuscarCliente").on("select2:select", function (e) {
        const data = e.params.data;

        $("#txtNombreCliente").val(data.text);
        $("#txtCorreoCliente").val(data.correo);
        $("#txtRfcCliente").val(data.rfc);
        $("#txtDomicilioFiscalCliente").val(data.domicilioFiscal);
        $("#txtRegimenFiscalCliente").val(data.regimenFiscal);
        $("#txtFechaRegistroCliente").val(data.fechaRegistro);
    });
});

// =====================================================
// ACTIVAR SELECTOR DE PRODUCTOS
// =====================================================
function activarSelectorProductos() {

    $("#cboBuscarProducto").select2({
        ajax: {
            url: "/Venta/ObtenerProductos",
            dataType: 'json',
            contentType: "application/json; charset=utf-8",
            delay: 250,
            data: params => ({ busqueda: params.term }),
            processResults: function (data) {
                return {
                    results: data.map(item => ({
                        id: item.idProducto,
                        text: item.descripcion,
                        marca: item.marca,
                        categoria: item.nombreCategoria,
                        urlImagen: item.urlImagen,
                        precio: parseFloat(item.precio),
                        descuento: parseFloat(item.descuento) || 0
                    }))
                };
            }
        },
        language: "es",
        placeholder: 'Buscar Producto...',
        minimumInputLength: 1,
        templateResult: formatoResultados
    });

    // =====================================================
    // SELECCIONAR PRODUCTO
    // =====================================================
    $("#cboBuscarProducto").on("select2:select", function (e) {

        if (!impuestoListo) {
            toastr.warning("", "Cargando impuesto, intenta en un segundo...");
            return;
        }

        const data = e.params.data;

        if (ProductosParaVenta.some(p => p.idProducto == data.id)) {
            $("#cboBuscarProducto").val("").trigger("change");
            toastr.warning("", "El producto ya fue agregado");
            return;
        }

        swal({
            title: data.marca,
            text: data.text,
            imageUrl: data.urlImagen,
            type: "input",
            showCancelButton: true,
            closeOnConfirm: false,
            inputPlaceholder: "Ingrese Cantidad"
        },
            function (valor) {

                if (valor === false) return false;

                if (valor === "" || isNaN(parseInt(valor))) {
                    toastr.warning("", "Ingrese una cantidad válida");
                    return false;
                }

                // CALCULAR PRECIO CON DESCUENTO AUTOMÁTICO
                let precioConDescuento = data.precio;
                if (data.descuento > 0) {
                    precioConDescuento = data.precio * (1 - data.descuento / 100);
                }

                let producto = {
                    idProducto: data.id,
                    marcaProducto: data.marca,
                    descripcionProducto: data.text,
                    categoriaProducto: data.categoria,
                    cantidad: parseInt(valor),
                    precio: parseFloat(data.precio),
                    precioConDescuento: parseFloat(precioConDescuento),
                    descuentoProducto: parseFloat(data.descuento) || 0,
                    total: parseFloat(valor) * parseFloat(precioConDescuento)
                };

                ProductosParaVenta.push(producto);
                mostrarProducto_Precios();
                $("#cboBuscarProducto").val("").trigger("change");
                swal.close();
            }
        );
    });
}

// =====================================================
// FORMATO VISUAL PARA LOS PRODUCTOS
// =====================================================
function formatoResultados(data) {

    if (data.loading)
        return data.text;

    return $(`
        <table width="100%">
            <tr>
                <td style="width:60px">
                    <img style="height:60px;width:60px;margin-right:10px" src="${data.urlImagen}" />
                </td>
                <td>
                    <p style="font-weight:bold;margin:2px">${data.marca}</p>
                    <p style="margin:2px">${data.text}</p>
                </td>
            </tr>
        </table>
    `);
}

// =====================================================
// MOSTRAR PRODUCTOS + CALCULAR SUBTOTAL, IVA Y TOTAL
// =====================================================
function mostrarProducto_Precios() {

    if (!ValorImpuesto || isNaN(ValorImpuesto)) {
        ValorImpuesto = 16;
    }

    let subtotalSinDescuento = 0;
    let subtotalConDescuento = 0;
    let descuentoTotalAplicado = 0;

    $("#tbProducto tbody").html("");

    ProductosParaVenta.forEach(item => {
        let totalSinDescuento = item.cantidad * item.precio;
        subtotalSinDescuento += totalSinDescuento;
        subtotalConDescuento += item.total;
        descuentoTotalAplicado += (totalSinDescuento - item.total);

        let subtotalProducto = item.cantidad * item.precioConDescuento;

        $("#tbProducto tbody").append(
            $("<tr>").append(
                $("<td>").append(
                    $("<button>").addClass("btn btn-danger btn-eliminar btn-sm").append(
                        $("<i>").addClass("fas fa-trash-alt")
                    ).data("idProducto", item.idProducto)
                ),
                $("<td>").text(item.descripcionProducto),
                $("<td>").text(item.cantidad),
                $("<td>").html(
                    item.descuentoProducto > 0 ?
                        `<span class="text-success">$${item.precioConDescuento.toFixed(2)}</span><br><small class="text-muted"><del>$${item.precio.toFixed(2)}</del></small>` :
                        `$${item.precio.toFixed(2)}`
                ),
                $("<td>").html(
                    item.descuentoProducto > 0 ?
                        `<span class="text-success">-${item.descuentoProducto}%</span>` :
                        "0%"
                ),
                $("<td>").text("$" + subtotalProducto.toFixed(2))
            )
        );
    });

    let porcentajeDescuentoTotal = 0;
    if (subtotalSinDescuento > 0) {
        porcentajeDescuentoTotal = (descuentoTotalAplicado / subtotalSinDescuento) * 100;
    }

    $("#txtDescuento").val(porcentajeDescuentoTotal.toFixed(2) + "%");

    let impuestoDecimal = ValorImpuesto / 100;
    let igv = subtotalConDescuento * impuestoDecimal;
    let total = subtotalConDescuento + igv;

    $("#txtSubTotal").val("$" + subtotalConDescuento.toFixed(2));
    $("#txtIGV").val("$" + igv.toFixed(2));
    $("#txtTotal").val("$" + total.toFixed(2));
}

// =====================================================
// ELIMINAR PRODUCTO
// =====================================================
$(document).on("click", "button.btn-eliminar", function () {
    const id = $(this).data("idProducto");
    ProductosParaVenta = ProductosParaVenta.filter(x => x.idProducto != id);
    mostrarProducto_Precios();
});

// =====================================================
// GUARDAR VENTA - CON VALIDACIÓN DE FACTURA
// =====================================================
$("#btnTerminarVenta").click(function () {

    if (ProductosParaVenta.length < 1) {
        toastr.warning("", "Debe ingresar productos");
        return;
    }

    const tipoDocumento = $("#cboTipoDocumento").val();

    //  SI ES FACTURA, VALIDAR DATOS FISCALES
    if (tipoDocumento == "2") {
        const rfcCliente = $("#txtRfcCliente").val().trim();
        const domicilioFiscal = $("#txtDomicilioFiscalCliente").val().trim();
        const regimenFiscal = $("#txtRegimenFiscalCliente").val().trim();

        if (!rfcCliente || !domicilioFiscal || !regimenFiscal) {
            swal({
                title: "Datos Fiscales Incompletos",
                text: "Para emitir una factura debe seleccionar un cliente con:\n• RFC\n• Domicilio Fiscal\n• Régimen Fiscal",
                type: "warning",
                confirmButtonText: "Entendido"
            });
            return;
        }
    }

    const venta = {
        idTipoDocumentoVenta: parseInt(tipoDocumento),
        nombreCliente: $("#txtNombreCliente").val(),
        idCliente: $("#cboBuscarCliente").val() || null,
        subtotal: $("#txtSubTotal").val().replace("$", ""),
        impuestoTotal: $("#txtIGV").val().replace("$", ""),
        total: $("#txtTotal").val().replace("$", ""),
        DetalleVenta: ProductosParaVenta
    };

    $("#btnTerminarVenta").LoadingOverlay("show");

    fetch("/Venta/RegistrarVenta", {
        method: "POST",
        headers: { "Content-Type": "application/json; charset=utf-8" },
        body: JSON.stringify(venta)
    })
        .then(res => res.ok ? res.json() : Promise.reject(res))
        .then(resJson => {

            $("#btnTerminarVenta").LoadingOverlay("hide");

            if (resJson.estado) {

                ProductosParaVenta = [];
                mostrarProducto_Precios();

                $("#txtNombreCliente").val("");
                $("#txtCorreoCliente").val("");
                $("#txtRfcCliente").val("");
                $("#txtDomicilioFiscalCliente").val("");
                $("#txtRegimenFiscalCliente").val("");
                $("#txtFechaRegistroCliente").val("");
                $("#txtDescuento").val("0%");
                $("#cboTipoDocumento").val("1");
                $("#cboBuscarCliente").val("").trigger("change");

                if (tipoDocumento == "2") {
                    // ES FACTURA
                    swal({
                        title: "¡Factura Timbrada!",
                        text: `Número de Venta: ${resJson.objeto.numeroVenta}\n\nLa factura se ha generado correctamente.`,
                        type: "success",
                        confirmButtonText: "Ver Factura",
                        showCancelButton: true,
                        cancelButtonText: "Cerrar"
                    }, function (isConfirm) {
                        if (isConfirm) {
                            window.open(`/Venta/DescargarFacturaPDF?numeroVenta=${resJson.objeto.numeroVenta}`, '_blank');
                        }
                    });
                } else {
                    // ES TICKET
                    swal({
                        title: "¡Registrado!",
                        text: `Número de Venta: ${resJson.objeto.numeroVenta}`,
                        type: "success",
                        showCancelButton: false,
                        confirmButtonText: "Continuar"
                    });
                }

            } else {
                swal("Error", resJson.mensaje || "No se pudo registrar la venta", "error");
            }

        })
        .catch(error => {
            $("#btnTerminarVenta").LoadingOverlay("hide");
            console.error("Error:", error);
            swal("Error", "Ocurrió un error al registrar la venta", "error");
        });
});
